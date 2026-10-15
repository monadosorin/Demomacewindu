using System.Collections.Generic;
using UnityEngine;

// On-screen readout of Mace's FSM: current state, transition log with the diagram's conditions, HP bars.
public class FsmDebugHUD : MonoBehaviour
{
    public MaceWinduAI mace;
    public EnemyAI enemy;
    public DemoDirector director;
    public int maxLogLines = 6;

    readonly List<string> log = new List<string>();
    GUIStyle panelStyle, titleStyle, stateStyle, textStyle, worldStyle;

    void Start()
    {
        if (mace == null) return;
        mace.StateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (mace != null) mace.StateChanged -= OnStateChanged;
    }

    void OnStateChanged(MaceWinduAI.State from, MaceWinduAI.State to, string reason)
    {
        log.Insert(0, $"{MaceWinduAI.Label(from)} -> <b>{MaceWinduAI.Label(to)}</b>  <color=#aaaaaa>({reason})</color>");
        if (log.Count > maxLogLines) log.RemoveAt(log.Count - 1);
    }

    void BuildStyles()
    {
        if (panelStyle != null) return;
        var bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
        bg.Apply();

        panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 6, 6) };
        panelStyle.normal.background = bg;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, richText = true };
        titleStyle.normal.textColor = new Color(0.75f, 0.6f, 1f);
        stateStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        stateStyle.normal.textColor = Color.white;
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, richText = true };
        textStyle.normal.textColor = Color.white;
        worldStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    void OnGUI()
    {
        if (mace == null || enemy == null) return;
        BuildStyles();

        GUILayout.BeginArea(new Rect(8, 8, 300, 300), panelStyle);
        GUILayout.Label("MACE WINDU  -  FSM", titleStyle);
        GUILayout.Label(mace.Health.IsDead ? "Defeated" : MaceWinduAI.Label(mace.CurrentState), stateStyle);
        Bar("Mace HP", mace.Health.Normalized, new Color(0.6f, 0.3f, 1f));
        Bar("Enemy HP", enemy.Health.Normalized, new Color(1f, 0.25f, 0.25f));
        GUILayout.Label($"Enemy: {enemy.ModeLabel}", textStyle);
        if (director != null)
            GUILayout.Label($"Score  Mace {director.MaceWins} : {director.EnemyWins} Enemy", textStyle);
        GUILayout.Space(6);
        GUILayout.Label("Transitions", titleStyle);
        foreach (var line in log) GUILayout.Label(line, textStyle);
        GUILayout.EndArea();

        WorldLabel(mace.transform, MaceWinduAI.Label(mace.CurrentState), new Color(0.8f, 0.65f, 1f));
        WorldLabel(enemy.transform, enemy.ModeLabel, new Color(1f, 0.5f, 0.5f));
    }

    void Bar(string label, float value, Color color)
    {
        GUILayout.Label(label, textStyle);
        var rect = GUILayoutUtility.GetRect(270, 8);
        var old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    void WorldLabel(Transform t, string text, Color color)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var screen = cam.WorldToScreenPoint(t.position + Vector3.up * 2.6f);
        if (screen.z <= 0f) return;
        worldStyle.normal.textColor = color;
        GUI.Label(new Rect(screen.x - 90f, Screen.height - screen.y - 12f, 180f, 24f), text, worldStyle);
    }
}
