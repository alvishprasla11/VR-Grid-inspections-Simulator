using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class DroneMissionWeather : MonoBehaviour
{
    const string PhotoPathKey = "GridSightFaultPhotoPath";
    const string ReturnScene = "GridSight_ControlRoom";

    InputAction captureAction;
    ParticleSystem snow;
    bool captureStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), new LoadSceneMode());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var existing = FindFirstObjectByType<DroneMissionWeather>();
        if (scene.name != "SampleScene") {
            if (existing != null) Destroy(existing.gameObject);
            return;
        }
        if (existing == null) {
            var host = new GameObject("DroneMissionWeather");
            DontDestroyOnLoad(host);
            host.AddComponent<DroneMissionWeather>();
        }
    }

    void Awake()
    {
        captureAction = new InputAction("CaptureFaultPhoto", InputActionType.Button,
            "<XRController>{LeftHand}/gripPressed");
        captureAction.AddBinding("<XRController>{RightHand}/gripPressed");
        captureAction.AddBinding("<OculusTouchController>{LeftHand}/gripPressed");
        captureAction.AddBinding("<OculusTouchController>{RightHand}/gripPressed");
        captureAction.Enable();
        CreateSnow();
        CreateSnowGround();
        DarkenStorm();
    }

    void Update()
    {
        if (!captureStarted && captureAction.WasPressedThisFrame())
            StartCoroutine(CaptureAndReturn());
    }

    void CreateSnow()
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        var snowObject = new GameObject("QuestSnowfall");
        snowObject.transform.SetParent(camera.transform, false);
        snowObject.transform.localPosition = new Vector3(0f, 0f, 0.65f);
        snow = snowObject.AddComponent<ParticleSystem>();
        var main = snow.main;
        main.loop = true;
        main.startLifetime = 3.5f;
        main.startSpeed = 0.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
        main.startColor = new Color(0.9f, 0.95f, 1f, 0.85f);
        main.maxParticles = 700;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = snow.emission;
        emission.rateOverTime = 420f;
        var shape = snow.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(2.4f, 1.8f, 1.2f);
        var velocity = snow.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(-1.1f, -0.5f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        var renderer = snow.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.mainTexture = CreateRoundSnowTexture();
        renderer.material.color = Color.white;
    }

    void DarkenStorm()
    {
        var camera = Camera.main;
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.025f, 0.06f, 1f);
        }
        RenderSettings.ambientIntensity = 0.2f;
        RenderSettings.ambientLight = new Color(0.08f, 0.11f, 0.2f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.07f, 0.1f, 0.18f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.045f;
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            light.intensity *= 0.55f;
    }

    static Texture2D CreateRoundSnowTexture()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                float alpha = Mathf.Clamp01((16f - distance) * 2.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        return texture;
    }

    void CreateSnowGround()
    {
        GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Plane);
        cover.name = "SnowAccumulation";
        cover.transform.position = new Vector3(0f, 0.035f, 0f);
        cover.transform.localScale = new Vector3(2.2f, 1f, 2.2f);
        var renderer = cover.GetComponent<Renderer>();
        renderer.material = SnowMaterial(new Color(0.82f, 0.88f, 0.94f, 0.9f));
        Destroy(cover.GetComponent<Collider>());

        var snowbankPositions = new[] {
            new Vector3(-5f, 0.15f, 4f), new Vector3(4f, 0.12f, 3f),
            new Vector3(-4f, 0.1f, -3f), new Vector3(5f, 0.14f, -4f),
            new Vector3(0f, 0.08f, 6f)
        };
        for (int i = 0; i < snowbankPositions.Length; i++)
        {
            var bank = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bank.name = $"Snowbank_{i + 1}";
            bank.transform.position = snowbankPositions[i];
            bank.transform.localScale = new Vector3(2.8f, 0.22f, 1.4f);
            bank.transform.rotation = Quaternion.Euler(0f, i * 31f, 0f);
            bank.GetComponent<Renderer>().material = SnowMaterial(new Color(0.9f, 0.94f, 1f, 1f));
            Destroy(bank.GetComponent<Collider>());
        }
    }

    static Material SnowMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                     Shader.Find("Standard") ??
                     Shader.Find("Unlit/Color");
        var material = new Material(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
        return material;
    }

    IEnumerator CaptureAndReturn()
    {
        captureStarted = true;
        string path = Path.Combine(Application.persistentDataPath, "gridsight_fault_photo.png");
        PlayerPrefs.SetString(PhotoPathKey, path);
        PlayerPrefs.SetString("GridSightRepairStatus", "DISPATCHED REPAIRMEN");
        PlayerPrefs.Save();
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path);
        float timeout = Time.realtimeSinceStartup + 8f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < timeout)
            yield return null;
        if (!File.Exists(path))
            Debug.LogError($"GridSight could not save the fault photo to {path}.", this);
        SceneManager.LoadScene(ReturnScene);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        captureAction?.Disable();
        captureAction?.Dispose();
    }
}
