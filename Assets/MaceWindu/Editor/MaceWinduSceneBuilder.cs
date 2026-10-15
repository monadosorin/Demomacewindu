using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Menu: Mace Windu > Build Arena Scene. Generates the rough grey-box demo scene from scratch.
public static class MaceWinduSceneBuilder
{
    const string Root = "Assets/MaceWindu";
    const string MaterialDir = Root + "/Materials";
    const string SceneDir = Root + "/Scenes";
    const string ScenePath = SceneDir + "/MaceWinduArena.unity";

    const float ArenaSize = 36f;

    [MenuItem("Mace Windu/Build Arena Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder(Root, "Materials");
        EnsureFolder(Root, "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ------------------------------------------------ materials (flat greys, plain coloured blocks for sabers)
        var floorMat = LitMaterial("M_Floor", new Color(0.5f, 0.5f, 0.5f));
        var wallMat = LitMaterial("M_Wall", new Color(0.35f, 0.35f, 0.35f));
        var maceMat = LitMaterial("M_Mace", new Color(0.75f, 0.75f, 0.75f));
        var enemyMat = LitMaterial("M_Enemy", new Color(0.25f, 0.25f, 0.25f));
        var darkMat = LitMaterial("M_Dark", new Color(0.05f, 0.05f, 0.05f));
        var markerMat = LitMaterial("M_PatrolMarker", new Color(0.3f, 0.45f, 0.7f));
        var purpleBlade = UnlitMaterial("M_SaberPurple", new Color(0.7f, 0.3f, 1f));
        var redBlade = UnlitMaterial("M_SaberRed", new Color(1f, 0.15f, 0.15f));
        var laserMat = UnlitMaterial("M_Laser", new Color(1f, 0.2f, 0.1f));
        var forceMat = UnlitMaterial("M_Force", new Color(0.4f, 0.8f, 1f));

        // ------------------------------------------------ one plain light, no shadows, no post-processing
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1f;
        sun.shadows = LightShadows.None;
        sun.transform.rotation = Quaternion.Euler(60f, -30f, 0f);

        // ------------------------------------------------ arena (everything under here gets baked into the NavMesh)
        var arena = new GameObject("Arena");
        Box("Floor", arena.transform, new Vector3(0f, -0.5f, 0f), new Vector3(ArenaSize, 1f, ArenaSize), floorMat);

        float half = ArenaSize * 0.5f;
        Box("Wall N", arena.transform, new Vector3(0f, 1.5f, half + 0.5f), new Vector3(ArenaSize + 2f, 3f, 1f), wallMat);
        Box("Wall S", arena.transform, new Vector3(0f, 0.5f, -half - 0.5f), new Vector3(ArenaSize + 2f, 1f, 1f), wallMat); // low so it doesn't block the camera
        Box("Wall E", arena.transform, new Vector3(half + 0.5f, 1.5f, 0f), new Vector3(1f, 3f, ArenaSize), wallMat);
        Box("Wall W", arena.transform, new Vector3(-half - 0.5f, 1.5f, 0f), new Vector3(1f, 3f, ArenaSize), wallMat);

        foreach (var p in new[] { new Vector3(7f, 0f, 7f), new Vector3(-7f, 0f, 7f), new Vector3(7f, 0f, -7f), new Vector3(-7f, 0f, -7f) })
            Box("Pillar", arena.transform, p + Vector3.up * 2f, new Vector3(2f, 4f, 2f), wallMat);

        var surface = arena.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        var navMeshPath = SceneDir + "/MaceWinduArena_NavMesh.asset";
        AssetDatabase.DeleteAsset(navMeshPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navMeshPath);

        // ------------------------------------------------ patrol route (the "titik stop" points)
        var route = new GameObject("Patrol Points");
        var patrolPositions = new[] { new Vector3(-11f, 0f, 0f), new Vector3(0f, 0f, 11f), new Vector3(11f, 0f, 0f), new Vector3(0f, 0f, -11f) };
        var patrolPoints = patrolPositions.Select((pos, i) =>
        {
            var point = new GameObject($"Stop Point {i + 1}").transform;
            point.SetParent(route.transform);
            point.position = pos;
            var marker = Part("Marker", point, new Vector3(0f, 0.02f, 0f), new Vector3(0.8f, 0.03f, 0.8f), markerMat);
            marker.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return point;
        }).ToArray();

        // ------------------------------------------------ fighters
        var maceGo = Character("Mace Windu", new Vector3(0f, 0f, -11f), Quaternion.identity, maceMat, darkMat, purpleBlade, out var maceSaber);
        var enemyGo = Character("Enemy", new Vector3(13f, 0f, 13f), Quaternion.Euler(0f, 225f, 0f), enemyMat, darkMat, redBlade, out var enemySaber);

        // Blaster in the enemy's off hand
        var blaster = Part("Blaster", enemyGo.transform, new Vector3(-0.5f, 1.05f, 0.35f), new Vector3(0.1f, 0.14f, 0.4f), darkMat);
        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(blaster.transform, false);
        muzzle.localPosition = new Vector3(0f, 0f, 0.6f);

        var mace = maceGo.AddComponent<MaceWinduAI>();
        var enemy = enemyGo.AddComponent<EnemyAI>();

        mace.enemy = enemy;
        mace.saber = maceSaber;
        mace.patrolPoints = patrolPoints;
        mace.forceMaterial = forceMat;

        enemy.target = mace;
        enemy.saber = enemySaber;
        enemy.muzzle = muzzle;
        enemy.laserMaterial = laserMat;

        // ------------------------------------------------ static camera over the whole arena
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        cam.fieldOfView = 50f;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0f, 32f, -30f);
        camGo.transform.LookAt(new Vector3(0f, 0f, 1f));

        var systems = new GameObject("Demo Systems");
        var director = systems.AddComponent<DemoDirector>();
        director.mace = mace;
        director.enemy = enemy;
        var hud = systems.AddComponent<FsmDebugHUD>();
        hud.mace = mace;
        hud.enemy = enemy;
        hud.director = director;

        // ------------------------------------------------ save
        EditorSceneManager.SaveScene(scene, ScenePath);
        var buildScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = maceGo;
        Debug.Log($"[Mace Windu] Arena scene built at {ScenePath}. Press Play.");
    }

    // ---------------------------------------------------------------- characters

    static GameObject Character(string name, Vector3 position, Quaternion rotation, Material body, Material dark,
        Material blade, out Lightsaber saber)
    {
        var root = new GameObject(name);
        root.transform.SetPositionAndRotation(position, rotation);

        Part("Leg L", root.transform, new Vector3(-0.2f, 0.3f, 0f), new Vector3(0.3f, 0.6f, 0.35f), body);
        Part("Leg R", root.transform, new Vector3(0.2f, 0.3f, 0f), new Vector3(0.3f, 0.6f, 0.35f), body);
        Part("Torso", root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.8f, 1.1f, 0.5f), body);
        Part("Head", root.transform, new Vector3(0f, 1.95f, 0f), new Vector3(0.45f, 0.45f, 0.45f), body);
        Part("Visor", root.transform, new Vector3(0f, 2f, 0.23f), new Vector3(0.35f, 0.08f, 0.02f), dark); // shows facing

        // Saber pivot sits at the right hand; the blade extends along local +Y.
        var pivot = new GameObject("Saber Pivot");
        pivot.transform.SetParent(root.transform, false);
        pivot.transform.localPosition = new Vector3(0.5f, 1.05f, 0.35f);
        Part("Hilt", pivot.transform, new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.28f, 0.07f), dark);
        Part("Blade", pivot.transform, new Vector3(0f, 0.7f, 0f), new Vector3(0.06f, 1.15f, 0.06f), blade);
        saber = pivot.AddComponent<Lightsaber>();

        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.45f;
        agent.height = 2.2f;
        agent.angularSpeed = 720f;
        agent.acceleration = 20f;

        root.AddComponent<Health>();
        return root;
    }

    // ---------------------------------------------------------------- primitives

    static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static GameObject Part(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    // ---------------------------------------------------------------- assets

    static Material LitMaterial(string name, Color baseColor)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", 0f);
        return SaveMaterial(mat, name);
    }

    static Material UnlitMaterial(string name, Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        return SaveMaterial(mat, name);
    }

    static Material SaveMaterial(Material mat, string name)
    {
        var path = $"{MaterialDir}/{name}.mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent))
        {
            var slash = parent.LastIndexOf('/');
            AssetDatabase.CreateFolder(parent.Substring(0, slash), parent.Substring(slash + 1));
        }
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }
}
