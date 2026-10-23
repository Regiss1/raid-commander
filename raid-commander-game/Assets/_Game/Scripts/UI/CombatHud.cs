using System;
using System.Globalization;
using System.Text;
using RaidCommander.Abilities;
using RaidCommander.Combat;
using RaidCommander.Selection;
using RaidCommander.Units;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RaidCommander.UI
{
    /// <summary>Screen-space presentation for the existing party, selection and combat services.</summary>
    public sealed class CombatHud : MonoBehaviour
    {
        [SerializeField] private UnitSelectionController selection;
        [SerializeField] private CombatUnit[] party = new CombatUnit[4];
        [SerializeField] private CombatUnit boss;
        [SerializeField] private CombatLogService log;
        [SerializeField] private DecisionLogService decisions;
        [SerializeField] private CombatDebugActions actions;
        [SerializeField] private InputActionAsset controls;

        private readonly Frame[] frames = new Frame[4];
        private readonly StringBuilder builder = new StringBuilder(8192);
        private readonly Color[] roles = {
            new Color(.16f, .45f, .88f), new Color(.18f, .72f, .36f),
            new Color(.93f, .55f, .17f), new Color(.08f, .77f, .84f)
        };
        private static readonly string[] RoleNames = { "Tank", "Healer", "Melee DPS", "Ranged DPS" };
        private GameObject normalView;
        private ScrollRect normalScroll;
        private TMP_Text normalText;
        private ScrollRect decisionScroll;
        private TMP_Text decisionText;
        private TMP_Text combatTabText, decisionTabText;
        private bool selectedDecisions;
        private bool followDecisions = true;
        private bool pendingDecisionFollow;
        private long firstCombatSequence, firstDecisionSequence;
        private InputAction visibilityAction;
        private bool worldBarsVisible = true;
        private bool followLog = true;
        private bool pendingFollow;
#if UNITY_EDITOR
        private GameObject debugView;
        private TMP_Text debugHistory;
        private TMP_Text debugStatus;
        private TMP_Text debugDiagnostics;
        private Toggle protection;
        private bool debugShown;
        private float nextDebugRefresh;
#endif

        private sealed class Frame
        {
            public CombatUnit Unit;
            public UnityEngine.UI.Image Border;
            public TMP_Text State;
        }

        private void Awake()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 15;
            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            BuildMeters();
            BuildParty();
            BuildLog();
            BuildAbilities();
#if UNITY_EDITOR
            BuildDebug();
            ApplyDebugMode();
#endif
            visibilityAction = controls != null ? controls.FindAction("Toggle Party World Health Bars") : null;
            SetWorldBars(true);
        }

        private void OnEnable()
        {
            if (selection != null) selection.SelectionChanged += OnSelectionChanged;
            if (log != null) { log.EntryRecorded += OnEntry; log.SessionReset += OnLogReset; }
            if (decisions != null) { decisions.EntryRecorded += OnDecision; decisions.SessionReset += OnDecisionReset; }
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] != null && frames[i].Unit != null)
                    frames[i].Unit.HealthChanged += OnHealthChanged;
            visibilityAction?.Enable();
            OnSelectionChanged(selection != null ? selection.SelectedUnit : null);
            RefreshAllHealth();
            RebuildLog(false);
            RebuildDecisions(false);
        }

        private void Start()
        {
            RefreshAllHealth();
            RebuildLog(false);
            RebuildDecisions(false);
        }

        private void OnDisable()
        {
            if (selection != null) selection.SelectionChanged -= OnSelectionChanged;
            if (log != null) { log.EntryRecorded -= OnEntry; log.SessionReset -= OnLogReset; }
            if (decisions != null) { decisions.EntryRecorded -= OnDecision; decisions.SessionReset -= OnDecisionReset; }
            foreach (Frame frame in frames)
                if (frame != null && frame.Unit != null) frame.Unit.HealthChanged -= OnHealthChanged;
            visibilityAction?.Disable();
        }

        private void Update()
        {
            if (visibilityAction != null && visibilityAction.WasPressedThisFrame())
                SetWorldBars(!worldBarsVisible);
#if UNITY_EDITOR
            if (debugShown != UnityEditor.SessionState.GetBool("RaidCommander.ShowCombatDebug", false)) ApplyDebugMode();
            if (debugShown && Time.unscaledTime >= nextDebugRefresh)
            {
                nextDebugRefresh = Time.unscaledTime + .2f;
                RefreshDebug();
            }
#endif
        }

        private void LateUpdate()
        {
            if (!followLog) pendingFollow = false;
            if (!followDecisions) pendingDecisionFollow = false;
            if (pendingFollow && normalScroll != null && normalScroll.gameObject.activeInHierarchy)
            {
                Canvas.ForceUpdateCanvases();
                normalScroll.verticalNormalizedPosition = 0f;
                pendingFollow = false;
            }
            if (pendingDecisionFollow && decisionScroll != null && decisionScroll.gameObject.activeInHierarchy)
            {
                Canvas.ForceUpdateCanvases();
                decisionScroll.verticalNormalizedPosition = 0f;
                pendingDecisionFollow = false;
            }
        }

        public void SetWorldBars(bool visible)
        {
            worldBarsVisible = visible;
            foreach (CombatUnit unit in party)
                if (unit != null)
                {
                    Transform bar = unit.transform.Find("World Health Bar");
                    if (bar != null) bar.gameObject.SetActive(visible);
                }
        }

        private void OnHealthChanged(CombatUnit unit)
        {
            foreach (Frame frame in frames)
                if (frame != null && frame.Unit == unit) RefreshHealth(frame);
        }

        private void RefreshAllHealth()
        {
            foreach (Frame frame in frames) if (frame != null) RefreshHealth(frame);
        }

        private static void RefreshHealth(Frame frame)
        {
            if (frame.State != null)
                frame.State.text = frame.Unit == null || !frame.Unit.IsAlive ? "Dead" : "";
        }

        private void OnSelectionChanged(SelectableUnit selected)
        {
            foreach (Frame frame in frames)
                if (frame != null && frame.Border != null)
                    frame.Border.color = selected != null && frame.Unit != null
                        && selected.gameObject == frame.Unit.gameObject
                        ? new Color(1f, .87f, .34f) : new Color(.14f, .2f, .28f);
        }

        private void BuildParty()
        {
            var panel = Panel("Party Frames", transform, new Vector2(.765f, .31f),
                new Vector2(.995f, .83f), new Color(.055f, .085f, .12f, .86f));
            Text("Party", panel.transform, 25, FontStyles.Bold, TextAlignmentOptions.Left,
                new Vector2(.04f, .91f), new Vector2(.95f, .99f));
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                float top = .89f - i * .215f;
                var border = Panel("Frame " + (i + 1), panel.transform,
                    new Vector2(.035f, top - .20f), new Vector2(.965f, top), new Color(.14f, .2f, .28f));
                var body = Panel("Body", border.transform, new Vector2(.008f, .035f),
                    new Vector2(.992f, .965f), new Color(.08f, .12f, .17f));
                var button = border.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() =>
                {
                    if (selection != null && party != null && index < party.Length && party[index] != null)
                        selection.Select(party[index].GetComponent<SelectableUnit>());
                });
                var icon = Panel("Role Icon", body.transform, new Vector2(.025f, .20f),
                    new Vector2(.16f, .80f), roles[i]);
                icon.raycastTarget = false;
                Text((i + 1).ToString(), icon.transform, 22, FontStyles.Bold,
                    TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
                Text(RoleNames[i], body.transform, 25, FontStyles.Bold, TextAlignmentOptions.Left,
                    new Vector2(.20f, .60f), new Vector2(.96f, .96f));
                var track = Panel("Missing Health", body.transform, new Vector2(.20f, .15f),
                    new Vector2(.96f, .57f), new Color(.13f, .17f, .20f));
                track.raycastTarget = false;
                var fill = Panel("Health Fill", track.transform, Vector2.zero, Vector2.one, roles[i]);
                fill.raycastTarget = false;
                fill.sprite = SpriteForFill();
                fill.type = UnityEngine.UI.Image.Type.Filled;
                fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                var hp = Text("0 / 0", track.transform, 24, FontStyles.Bold,
                    TextAlignmentOptions.Right, new Vector2(.01f, 0f), new Vector2(.98f, 1f));
                var dead = Text("", track.transform, 24, FontStyles.Bold,
                    TextAlignmentOptions.Left, new Vector2(.02f, 0f), new Vector2(.5f, 1f));
                var binding = border.gameObject.AddComponent<HealthBarBinding>();
                binding.ConfigurePresentation(fill, hp);
                CombatUnit unit = party != null && i < party.Length ? party[i] : null;
                binding.Bind(unit);
                frames[i] = new Frame { Unit = unit, Border = border, State = dead };
            }
        }

        private void BuildMeters()
        {
            var panel = Panel("Damage Meters Mock", transform, new Vector2(.005f, .77f),
                new Vector2(.235f, .90f), new Color(.055f, .085f, .12f, .9f));
            Text("Damage Meters  -  Mock", panel.transform, 24, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(.04f, .72f), new Vector2(.97f, .98f));
            string[] samples = { "#1 Sample", "#2 Sample", "#3 Sample" };
            for (int i = 0; i < 3; i++)
            {
                float top = .68f - .21f * i;
                var bar = Panel(samples[i], panel.transform, new Vector2(.06f, top - .17f),
                    new Vector2(.91f - i * .15f, top), roles[i]);
                bar.raycastTarget = false;
                Text(samples[i], bar.transform, 17, FontStyles.Bold, TextAlignmentOptions.Left,
                    new Vector2(.03f, 0f), Vector2.one);
            }
        }

        private void BuildAbilities()
        {
            var panel = Panel("Group Abilities Mock", transform, new Vector2(.25f, .005f),
                new Vector2(.75f, .19f), new Color(.055f, .085f, .12f, .92f));
            Text("WIP: Group Abilities  -  Disabled", panel.transform, 19, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(.02f, .71f), new Vector2(.98f, .98f));
            string[] slots = { "Group\nHeal", "DPS\nBoost", "WIP", "WIP", "Special",
                "Class\nAbility 1", "Class\nAbility 2", "Class\nAbility 3", "Class\nAbility 4" };
            for (int i = 0; i < slots.Length; i++)
            {
                float left = .014f + i * .109f;
                var slot = Panel("Disabled " + slots[i].Replace('\n', ' '), panel.transform,
                    new Vector2(left, .08f), new Vector2(left + .103f, .68f), new Color(.15f, .19f, .23f));
                slot.raycastTarget = false;
                Text(slots[i], slot.transform, 16, FontStyles.Normal, TextAlignmentOptions.Center,
                    new Vector2(.02f, 0f), new Vector2(.98f, 1f));
            }
        }

        private void BuildLog()
        {
            normalView = Panel("Combat Log", transform, new Vector2(.005f, .035f),
                new Vector2(.235f, .755f), new Color(.055f, .085f, .12f, .91f)).gameObject;
            Text("Encounter Log", normalView.transform, 23, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(.04f, .945f), new Vector2(.96f, .995f));
            var combatTab = Panel("Combat Tab", normalView.transform, new Vector2(.035f, .885f),
                new Vector2(.49f, .94f), new Color(.16f, .29f, .41f));
            var decisionTab = Panel("Decisions Tab", normalView.transform, new Vector2(.51f, .885f),
                new Vector2(.965f, .94f), new Color(.12f, .18f, .24f));
            combatTab.gameObject.AddComponent<Button>().onClick.AddListener(() => SetTab(false));
            decisionTab.gameObject.AddComponent<Button>().onClick.AddListener(() => SetTab(true));
            combatTabText = Text("Combat", combatTab.transform, 21, FontStyles.Bold,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            decisionTabText = Text("Decisions", decisionTab.transform, 21, FontStyles.Bold,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            normalScroll = CreateScroll(normalView.transform, new Vector2(.035f, .025f),
                new Vector2(.965f, .88f), out RectTransform content);
            normalScroll.onValueChanged.AddListener(_ =>
            {
                if (normalScroll == null || !normalScroll.gameObject.activeInHierarchy || Mouse.current == null) return;
                Vector2 pointer = Mouse.current.position.ReadValue();
                if (!RectTransformUtility.RectangleContainsScreenPoint(normalScroll.viewport, pointer)) return;
                bool wheel = Mathf.Abs(Mouse.current.scroll.ReadValue().y) > .01f;
                bool drag = Mouse.current.leftButton.isPressed;
                if (wheel || drag) followLog = normalScroll.verticalNormalizedPosition <= .02f;
            });
            normalText = Text("", content, 24, FontStyles.Normal, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), Vector2.one);
            normalText.textWrappingMode = TextWrappingModes.Normal;
            normalText.overflowMode = TextOverflowModes.Overflow;
            normalText.rectTransform.pivot = new Vector2(.5f, 1f);
            var fitter = normalText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            normalScroll.content = normalText.rectTransform;

            decisionScroll = CreateScroll(normalView.transform, new Vector2(.035f, .025f),
                new Vector2(.965f, .88f), out RectTransform decisionContent);
            decisionScroll.gameObject.name = "Decisions Scroll";
            decisionScroll.onValueChanged.AddListener(_ =>
            {
                if (!decisionScroll.gameObject.activeInHierarchy || Mouse.current == null) return;
                Vector2 pointer = Mouse.current.position.ReadValue();
                if (!RectTransformUtility.RectangleContainsScreenPoint(decisionScroll.viewport, pointer)) return;
                bool wheel = Mathf.Abs(Mouse.current.scroll.ReadValue().y) > .01f;
                bool drag = Mouse.current.leftButton.isPressed;
                if (wheel || drag) followDecisions = decisionScroll.verticalNormalizedPosition <= .02f;
            });
            decisionText = Text("", decisionContent, 21, FontStyles.Normal, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), Vector2.one);
            decisionText.textWrappingMode = TextWrappingModes.Normal;
            decisionText.overflowMode = TextOverflowModes.Overflow;
            decisionText.rectTransform.pivot = new Vector2(.5f, 1f);
            decisionText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            decisionScroll.content = decisionText.rectTransform;
            SetTab(false);
        }

        public bool DecisionsSelected => selectedDecisions;
        private void SetTab(bool showDecisions)
        {
            selectedDecisions = showDecisions;
            normalScroll.gameObject.SetActive(!showDecisions);
            decisionScroll.gameObject.SetActive(showDecisions);
            combatTabText.text = showDecisions ? "Combat" : "> Combat";
            decisionTabText.text = showDecisions ? "> Decisions" : "Decisions";
            combatTabText.transform.parent.GetComponent<UnityEngine.UI.Image>().color = showDecisions
                ? new Color(.12f, .18f, .24f) : new Color(.16f, .29f, .41f);
            decisionTabText.transform.parent.GetComponent<UnityEngine.UI.Image>().color = showDecisions
                ? new Color(.16f, .29f, .41f) : new Color(.12f, .18f, .24f);
            if (showDecisions)
            {
                RebuildDecisions(false);
                if (followDecisions) pendingDecisionFollow = true;
            }
            else
            {
                RebuildLog(false);
                if (followLog) pendingFollow = true;
            }
        }

        private void OnEntry(CombatLogEntry entry) => RebuildLog(false);
        private void OnLogReset() => RebuildLog(true);
        private void OnDecision(DecisionLogEntry entry) => RebuildDecisions(false);
        private void OnDecisionReset() => RebuildDecisions(true);

        private void RebuildLog(bool reset)
        {
            if (normalText == null || log == null || log.History == null) return;
            bool follow = reset || followLog;
            if (reset) followLog = true;
            float oldY = normalScroll.content.anchoredPosition.y;
            long newFirst = log.History.Count > 0 ? log.History[0].Sequence : 0;
            float removedHeight = EvictedHeight(normalText, firstCombatSequence, newFirst);
            builder.Clear();
            foreach (CombatLogEntry entry in log.History)
            {
                builder.Append(entry.Timestamp.ToString("F1", CultureInfo.InvariantCulture)).Append("  ");
                builder.Append(NameFor(entry.SourceId)).Append(" -> ").Append(NameFor(entry.TargetId));
                builder.Append("  ").Append(entry.Type);
                if (entry.SpellId.HasValue) builder.Append("  spell:").Append(entry.SpellId.Value);
                if (entry.Damage.HasValue) builder.Append("  -").Append(entry.Damage.Value);
                if (entry.Healing.HasValue) builder.Append("  +").Append(entry.Healing.Value);
                builder.Append('\n');
            }
            normalText.text = builder.Length == 0 ? "No combat events yet." : builder.ToString();
            firstCombatSequence = newFirst;
            Canvas.ForceUpdateCanvases();
            if (follow) pendingFollow = true;
            else normalScroll.content.anchoredPosition = new Vector2(0f, Mathf.Max(0f, oldY - removedHeight));
#if UNITY_EDITOR
            if (debugHistory != null)
            {
                debugHistory.text = normalText.text;
                debugHistory.GetComponent<LayoutElement>().preferredHeight =
                    Mathf.Max(200f, debugHistory.preferredHeight + 8f);
            }
#endif
        }

        private void RebuildDecisions(bool reset)
        {
            if (decisionText == null) return;
            if (reset) followDecisions = true;
            bool follow = followDecisions;
            float oldY = decisionScroll.content.anchoredPosition.y;
            long newFirst = decisions != null && decisions.History != null && decisions.History.Count > 0
                ? decisions.History[0].Sequence : 0;
            float removedHeight = EvictedHeight(decisionText, firstDecisionSequence, newFirst);
            builder.Clear();
            if (decisions != null && decisions.History != null)
                foreach (DecisionLogEntry entry in decisions.History)
                {
                    builder.Append(FormatTime(entry.Timestamp)).Append(' ').Append(entry.MemberName)
                        .Append(" | ").Append(entry.Hazards).Append(" | ");
                    if (entry.Kind == DecisionKind.AwarenessCheck)
                        builder.Append("Awareness ").Append(entry.Awareness.Value).Append(" | ")
                            .Append(entry.Awareness.Value * 10).Append("% | roll ")
                            .Append(entry.Roll.Value).Append("/10 | ")
                            .Append(entry.Success == true ? "Detected" : "Failed");
                    else if (entry.Kind == DecisionKind.EscapeStarted) builder.Append("Escape started");
                    else if (entry.Kind == DecisionKind.NoSafeExit) builder.Append("No safe exit");
                    else builder.Append("Manual order suppressed awareness/escape");
                    builder.Append('\n');
                }
            decisionText.text = builder.Length == 0 ? "No Awareness decisions yet." : builder.ToString();
            firstDecisionSequence = newFirst;
            Canvas.ForceUpdateCanvases();
            if (follow) pendingDecisionFollow = true;
            else decisionScroll.content.anchoredPosition = new Vector2(0f, Mathf.Max(0f, oldY - removedHeight));
        }

        private static string FormatTime(double seconds)
            => TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\.f", CultureInfo.InvariantCulture);

        private static float EvictedHeight(TMP_Text text, long oldFirst, long newFirst)
        {
            if (oldFirst <= 0 || newFirst <= oldFirst) return 0f;
            string old = text.text;
            float height = 0f;
            int start = 0;
            for (long i = oldFirst; i < newFirst && start < old.Length; i++)
            {
                int end = old.IndexOf('\n', start);
                if (end < 0) break;
                height += text.GetPreferredValues(old.Substring(start, end - start),
                    text.rectTransform.rect.width, Mathf.Infinity).y;
                start = end + 1;
            }
            return height;
        }

        private string NameFor(int? id)
        {
            if (!id.HasValue) return "Environment";
            foreach (CombatUnit unit in party)
                if (unit != null && unit.EncounterId == id.Value) return unit.name;
            if (boss != null && boss.EncounterId == id.Value) return boss.name;
            return "Unit " + id.Value;
        }

#if UNITY_EDITOR
        private void BuildDebug()
        {
            debugView = Panel("Combat Debug", transform, new Vector2(.005f, .035f),
                new Vector2(.235f, .755f), new Color(.055f, .085f, .12f, .96f)).gameObject;
            Text("Combat Debug", debugView.transform, 25, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(.04f, .93f), new Vector2(.96f, .99f));
            CreateScroll(debugView.transform, new Vector2(.035f, .025f), new Vector2(.965f, .925f),
                out RectTransform content);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 7f;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            debugDiagnostics = DebugText(content, 130f);
            var toggleRoot = Panel("Boss Protection", content, Vector2.zero, Vector2.one, new Color(.15f, .19f, .23f));
            toggleRoot.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
            protection = toggleRoot.gameObject.AddComponent<Toggle>();
            var mark = Panel("Check", toggleRoot.transform, new Vector2(.02f, .20f),
                new Vector2(.13f, .80f), new Color(.85f, .72f, .25f));
            protection.graphic = mark;
            protection.targetGraphic = toggleRoot;
            Text("Keep Boss Alive (min 1 HP)", toggleRoot.transform, 17, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(.16f, 0f), Vector2.one);
            protection.onValueChanged.AddListener(value => { if (actions != null) actions.KeepBossAlive = value; });
            DebugButton(content, "Tank -> Boss", () => actions?.TankMelee());
            DebugButton(content, "Healer -> Tank", () => actions?.HealerHeal());
            DebugButton(content, "Melee DPS -> Boss", () => actions?.DpsMelee());
            DebugButton(content, "Boss -> Tank", () => actions?.DamageTank());
            DebugButton(content, "Ranged DPS -> Boss", () => actions?.RangedDpsAttack());
            DebugButton(content, "Kill Boss", () => actions?.KillBoss());
            DebugButton(content, "Reset Health + Log", () => actions?.ResetEncounter());
            debugStatus = DebugText(content, 60f);
            debugHistory = DebugText(content, 700f);
            debugHistory.textWrappingMode = TextWrappingModes.Normal;
        }

        private TMP_Text DebugText(Transform parent, float height)
        {
            var result = Text("", parent, 24, FontStyles.Normal, TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one);
            result.textWrappingMode = TextWrappingModes.Normal;
            result.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return result;
        }

        private void DebugButton(Transform parent, string label, Action action)
        {
            var background = Panel(label, parent, Vector2.zero, Vector2.one, new Color(.18f, .28f, .36f));
            background.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            var button = background.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => { action(); RefreshDebug(); });
            Text(label, background.transform, 24, FontStyles.Normal, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);
        }

        private void ApplyDebugMode()
        {
            debugShown = UnityEditor.SessionState.GetBool("RaidCommander.ShowCombatDebug", false);
            normalView.SetActive(!debugShown);
            debugView.SetActive(debugShown);
            if (debugShown) RefreshDebug();
            else
            {
                SetTab(selectedDecisions);
                RebuildLog(false);
                RebuildDecisions(false);
            }
        }

        private void RefreshDebug()
        {
            if (actions == null || debugDiagnostics == null) return;
            if (protection.isOn != actions.KeepBossAlive)
                protection.SetIsOnWithoutNotify(actions.KeepBossAlive);
            builder.Clear();
            foreach (CombatUnit unit in party)
                if (unit != null)
                {
                    var brain = unit.GetComponent<PartyRoleBrain>();
                    if (brain != null) builder.Append(unit.name).Append(": ").Append(brain.State)
                        .Append(brain.Target != null ? " -> " + brain.Target.name : "").Append('\n');
                }
            if (boss != null)
            {
                var attack = boss.GetComponent<BossBasicAttack>();
                if (attack != null) builder.Append("Boss: ").Append(attack.State)
                    .Append(attack.CombatTarget != null ? " -> " + attack.CombatTarget.name : "").Append('\n');
                var ability = boss.GetComponent<BossAbilityRunner>();
                if (ability != null)
                {
                    if (ability.IsCasting && ability.ActiveDefinition != null)
                        builder.Append(ability.ActiveDefinition.DisplayName).Append("  ")
                            .Append(Mathf.Max(0f, ability.CastEndsAt - Time.time).ToString("F1")).Append("s\n");
                    if (!string.IsNullOrEmpty(ability.SetupError)) builder.Append(ability.SetupError);
                }
            }
            debugDiagnostics.text = builder.ToString();
            debugStatus.text = actions.LastStatus;
        }
#endif

        private ScrollRect CreateScroll(Transform parent, Vector2 min, Vector2 max,
            out RectTransform content)
        {
            var root = Panel("Scroll", parent, min, max, new Color(.025f, .045f, .07f, .8f));
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            var viewport = Panel("Viewport", root.transform, Vector2.zero, Vector2.one,
                Color.white);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var item = new GameObject("Content", typeof(RectTransform));
            item.transform.SetParent(viewport.transform, false);
            content = (RectTransform)item.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1f);
            content.offsetMin = new Vector2(6f, 0f);
            content.offsetMax = new Vector2(-6f, 0f);
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            return scroll;
        }

        private static UnityEngine.UI.Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text Text(string value, Transform parent, float size, FontStyles style,
            TextAlignmentOptions alignment, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite SpriteForFill()
        {
            // The saved health-bar sprite is also used by the world bars and boss HUD.
            var bar = UnityEngine.Object.FindFirstObjectByType<ScreenBossHealthBar>();
            var binding = bar != null ? bar.GetComponent<HealthBarBinding>() : null;
            return binding != null && binding.FillImage != null ? binding.FillImage.sprite : null;
        }

    }
}
