using System.Collections;
using System.Collections.Generic;
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
    float captureReadyAt;

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
        captureReadyAt = Time.unscaledTime + 1f;
        CreateSnow();
        CreateSnowGround();
        DarkenStorm();
        RandomizeElectricityVfx();
    }

    void Update()
    {
        if (!captureStarted && Time.unscaledTime >= captureReadyAt && captureAction.WasPressedThisFrame())
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
        main.startLifetime = 4.5f;
        main.startSpeed = 0.45f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
        main.startColor = new Color(0.82f, 0.9f, 1f, 0.38f);
        main.maxParticles = 450;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = snow.emission;
        emission.rateOverTime = 220f;
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
        renderer.material.color = new Color(0.88f, 0.94f, 1f, 0.7f);
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

    void RandomizeElectricityVfx()
    {
        var candidates = new List<GameObject>();
        var particleSystems = FindObjectsByType<ParticleSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var particleSystem in particleSystems)
        {
            Transform candidate = particleSystem.transform;
            Transform electricityParent = FindElectricityParent(candidate);
            if (electricityParent != null && electricityParent != candidate)
            {
                while (candidate.parent != null && candidate.parent != electricityParent)
                    candidate = candidate.parent;
            }
            else if (!HasElectricityName(candidate.name))
            {
                continue;
            }

            if (!candidates.Contains(candidate.gameObject))
                candidates.Add(candidate.gameObject);
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("GridSight found no electricity VFX. Name the four effects with 'Electricity' or place them under an 'Electricity' parent.", this);
            return;
        }

        foreach (var candidate in candidates)
            candidate.SetActive(false);

        int selectedIndex = Random.Range(0, candidates.Count);
        candidates[selectedIndex].SetActive(true);
        Debug.Log($"GridSight selected electricity outage VFX {selectedIndex + 1} of {candidates.Count}.", this);
    }

    static Transform FindElectricityParent(Transform current)
    {
        while (current != null)
        {
            if (HasElectricityName(current.name))
                return current;
            current = current.parent;
        }
        return null;
    }

    static bool HasElectricityName(string objectName)
    {
        return objectName.IndexOf("electricity", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("electric", System.StringComparison.OrdinalIgnoreCase) >= 0;
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
                float alpha = Mathf.Clamp01((16f - distance) * 1.4f) * 0.62f;
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
        yield return new WaitForEndOfFrame();
        var camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("GridSight could not capture the fault photo because no main camera is available.", this);
            captureStarted = false;
            yield break;
        }

        var texture = CaptureCameraFrame(camera);
        if (texture == null)
        {
            Debug.LogError("GridSight could not capture the fault photo from the drone camera.", this);
            captureStarted = false;
            yield break;
        }

        byte[] imageBytes = texture.EncodeToPNG();
        Destroy(texture);
        if (imageBytes == null || imageBytes.Length == 0)
        {
            Debug.LogError("GridSight produced an empty fault photo.", this);
            captureStarted = false;
            yield break;
        }

        try
        {
            File.WriteAllBytes(path, imageBytes);
        }
        catch (System.IO.IOException exception)
        {
            Debug.LogError($"GridSight could not save the fault photo to {path}: {exception.Message}", this);
            captureStarted = false;
            yield break;
        }
        catch (System.UnauthorizedAccessException exception)
        {
            Debug.LogError($"GridSight has no permission to save the fault photo to {path}: {exception.Message}", this);
            captureStarted = false;
            yield break;
        }
        PlayerPrefs.SetString(PhotoPathKey, path);
        PlayerPrefs.SetString("GridSightRepairStatus", "DISPATCHED REPAIRMEN");
        PlayerPrefs.Save();
        if (!Application.CanStreamedLevelBeLoaded(ReturnScene))
        {
            Debug.LogError($"GridSight could not return to control room scene '{ReturnScene}'. Add it to Build Settings.", this);
            captureStarted = false;
            yield break;
        }
        captureAction.Disable();
        SceneManager.LoadScene(ReturnScene);
        Destroy(gameObject);
    }

    static Texture2D CaptureCameraFrame(Camera camera)
    {
        const int width = 1024;
        const int height = 1024;
        var renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            texture.Apply(false, false);
            return texture;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    void OnDestroy()
    {
        captureAction?.Disable();
        captureAction?.Dispose();
    }

    void OnApplicationQuit()
    {
        string path = PlayerPrefs.GetString(PhotoPathKey, "");
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            File.Delete(path);

        PlayerPrefs.DeleteKey(PhotoPathKey);
        PlayerPrefs.DeleteKey("GridSightRepairStatus");
        PlayerPrefs.Save();
    }
}
