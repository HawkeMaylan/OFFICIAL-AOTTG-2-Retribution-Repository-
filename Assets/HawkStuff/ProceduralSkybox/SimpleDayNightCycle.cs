using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class SimpleDayNightCycle : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("References")]
    public Light sun;
    public Light moon;

    [Header("Time Settings")]
    public float dayDuration = 120f;
    [Range(0, 1)] public float sunriseDuration = 0.05f;
    [Range(0, 1)] public float middayDuration = 0.5f;
    [Range(0, 1)] public float sunsetDuration = 0.05f;
    [Range(0, 1)] public float nightDuration = 0.4f;

    [Header("Time Control")]
    [Tooltip("Current time of day (0-1)")]
    [Range(0, 1)] public float timeOfDay = 0f;
    public bool isTimePaused = false;
    [SerializeField] private bool allowTimeEditingInRuntime = true;

    [Header("Lighting Settings")]
    public float maxSunIntensity = 1.0f;
    public float maxMoonIntensity = 0.2f;
    public float minimumNightExposure = 0.2f;
    public float maximumDayExposure = 1.3f;

    [Header("Sunrise/Sunset Boost")]
    public float sunriseIntensityMultiplier = 1.5f;
    public float sunsetIntensityMultiplier = 1.5f;
    [Range(0f, 1f)] public float sunriseSunsetSaturationBoost = 0.3f;

    [Header("Update Intervals (Seconds)")]
    public float giUpdateInterval = 2f;
    public float materialUpdateInterval = 1f;
    public float skyboxReapplyInterval = 5f;

    [Header("Sun Direction")]
    public float sunAzimuth = 170f;
    public float sunInitialAngle = 0f;

    [Header("Sun Colors")]
    public Color sunriseSunColor = new Color(1.0f, 0.5f, 0.2f);
    public Color middaySunColor = Color.white;
    public Color sunsetSunColor = new Color(1.0f, 0.5f, 0.2f);

    [Header("Skybox Colors")]
    public Gradient skyColorByAngle;
    public Gradient groundColorByAngle;

    [Header("Multiplayer Sync")]
    public float syncInterval = 5f;
    private float _lastSyncTime = 0f;
    private float _lastGIUpdate = 0f;
    private float _lastMaterialUpdate = 0f;
    private float _lastSkyboxCheck = 0f;

    [SerializeField] private Material skyboxMaterial;
    private Color lastSkyTint, lastGroundColor;
    private float lastSentTimeOfDay = -1f;

    private void Start()
    {
        NormalizeDurations();
        InitializeSkybox();
    }

    private void InitializeSkybox()
    {
        if (skyboxMaterial == null)
            skyboxMaterial = Resources.Load<Material>("HawkProcedural");

        if (skyboxMaterial != null)
        {
            RenderSettings.skybox = skyboxMaterial;
            DynamicGI.UpdateEnvironment();
        }

        var cam = Camera.main;
        if (cam != null)
        {
            Skybox sb = cam.GetComponent<Skybox>();
            if (sb) sb.material = null;
        }
    }

    private void Update()
    {
        // Only update time if not paused and we're master client or in single player
        if (!isTimePaused && (PhotonNetwork.IsMasterClient || !PhotonNetwork.IsConnected))
        {
            timeOfDay += Time.deltaTime / dayDuration;
            if (timeOfDay > 1f) timeOfDay -= 1f;
        }

        // Check if time was manually edited in inspector
        if (allowTimeEditingInRuntime && lastSentTimeOfDay != timeOfDay)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                photonView.RPC("SyncLightingToClients", RpcTarget.All, timeOfDay);
            }
            lastSentTimeOfDay = timeOfDay;
        }
        // Regular sync for master client
        else if (PhotonNetwork.IsMasterClient && Time.time - _lastSyncTime >= syncInterval)
        {
            photonView.RPC("SyncLightingToClients", RpcTarget.Others, timeOfDay);
            _lastSyncTime = Time.time;
            lastSentTimeOfDay = timeOfDay;
        }

        UpdateSkyboxCheck();
        UpdateSunAndMoon();
        UpdateLighting();
    }

    private void UpdateSkyboxCheck()
    {
        if (skyboxMaterial != null && Time.time - _lastSkyboxCheck >= skyboxReapplyInterval)
        {
            if (RenderSettings.skybox != skyboxMaterial)
            {
                RenderSettings.skybox = skyboxMaterial;
                DynamicGI.UpdateEnvironment();
            }
            _lastSkyboxCheck = Time.time;
        }
    }

    private void NormalizeDurations()
    {
        float total = sunriseDuration + middayDuration + sunsetDuration + nightDuration;
        if (Mathf.Abs(total - 1f) > 0.01f)
        {
            float scale = 1f / total;
            sunriseDuration *= scale;
            middayDuration *= scale;
            sunsetDuration *= scale;
            nightDuration *= scale;
        }
    }

    private void UpdateSunAndMoon()
    {
        float sunAngle = GetSunAngle(timeOfDay) + sunInitialAngle;
        sun.transform.rotation = Quaternion.Euler(sunAngle, sunAzimuth, 0f);
        moon.transform.rotation = Quaternion.Euler(sunAngle + 180f, sunAzimuth, 0f);
    }

    private float GetSunAngle(float t)
    {
        float time = t * dayDuration;

        float sunriseTime = sunriseDuration * dayDuration;
        float middayTime = middayDuration * dayDuration;
        float sunsetTime = sunsetDuration * dayDuration;
        float nightTime = nightDuration * dayDuration;

        float sunriseEnd = sunriseTime;
        float middayEnd = sunriseEnd + middayTime;
        float sunsetEnd = middayEnd + sunsetTime;

        if (time < sunriseEnd)
            return Mathf.Lerp(0f, 15f, time / sunriseTime);
        else if (time < middayEnd)
            return Mathf.Lerp(15f, 165f, (time - sunriseEnd) / middayTime);
        else if (time < sunsetEnd)
            return Mathf.Lerp(165f, 180f, (time - middayEnd) / sunsetTime);
        else
            return Mathf.Lerp(180f, 360f, (time - sunsetEnd) / nightTime);
    }

    private void UpdateLighting()
    {
        float sunAngle = GetSunAngle(timeOfDay);
        float normalizedAngle = sunAngle / 360f;

        if (skyboxMaterial != null && Time.time - _lastMaterialUpdate >= materialUpdateInterval)
        {
            Color skyTint = skyColorByAngle.Evaluate(normalizedAngle);
            Color groundColor = groundColorByAngle.Evaluate(normalizedAngle);

            if (skyTint != lastSkyTint)
            {
                skyboxMaterial.SetColor("_SkyTint", skyTint);
                lastSkyTint = skyTint;
            }

            if (groundColor != lastGroundColor)
            {
                skyboxMaterial.SetColor("_GroundColor", groundColor);
                lastGroundColor = groundColor;
            }

            skyboxMaterial.SetFloat("_Exposure", CalculateExposureFromAngle(sunAngle));
            _lastMaterialUpdate = Time.time;
        }

        if (Time.time - _lastGIUpdate >= giUpdateInterval)
        {
            DynamicGI.UpdateEnvironment();
            _lastGIUpdate = Time.time;
        }

        float boostedIntensity = 0f;
        float currentSunAngle = sunAngle % 360f;

        // Sunrise boost (0-15 degrees)
        if (currentSunAngle < 15f)
        {
            float sunriseProgress = currentSunAngle / 15f;
            boostedIntensity = Mathf.Lerp(0f, maxSunIntensity * sunriseIntensityMultiplier, sunriseProgress);
        }
        // Transition from sunrise boost to normal (15-30 degrees)
        else if (currentSunAngle < 30f)
        {
            float transitionProgress = (currentSunAngle - 15f) / 15f;
            boostedIntensity = Mathf.Lerp(maxSunIntensity * sunriseIntensityMultiplier, maxSunIntensity, transitionProgress);
        }
        // Normal midday intensity (30-150 degrees)
        else if (currentSunAngle < 150f)
        {
            boostedIntensity = maxSunIntensity;
        }
        // Transition to sunset boost (150-165 degrees)
        else if (currentSunAngle < 165f)
        {
            float transitionProgress = (currentSunAngle - 150f) / 15f;
            boostedIntensity = Mathf.Lerp(maxSunIntensity, maxSunIntensity * sunsetIntensityMultiplier, transitionProgress);
        }
        // Sunset boost (165-180 degrees)
        else if (currentSunAngle < 180f)
        {
            float sunsetProgress = (currentSunAngle - 165f) / 15f;
            boostedIntensity = Mathf.Lerp(maxSunIntensity * sunsetIntensityMultiplier, 0f, sunsetProgress);
        }
        // Night time (180-360 degrees)
        else
        {
            boostedIntensity = 0f;
        }

        sun.intensity = boostedIntensity;
        moon.intensity = Mathf.Clamp01(-Vector3.Dot(sun.transform.forward, Vector3.down)) * maxMoonIntensity;
        sun.color = CalculateSunColorFromAngle(sunAngle);
    }

    private Color CalculateSunColorFromAngle(float angle)
    {
        Color baseColor;

        if (angle < 15f)
            baseColor = Color.Lerp(sunriseSunColor, middaySunColor, Mathf.InverseLerp(0f, 15f, angle));
        else if (angle < 165f)
            baseColor = middaySunColor;
        else if (angle < 180f)
            baseColor = Color.Lerp(middaySunColor, sunsetSunColor, Mathf.InverseLerp(165f, 180f, angle));
        else
            baseColor = sunsetSunColor;

        float saturationBoost = 0f;

        if (angle < 30f)
            saturationBoost = Mathf.Lerp(sunriseSunsetSaturationBoost, 0f, Mathf.InverseLerp(15f, 30f, angle));
        else if (angle >= 150f && angle < 165f)
            saturationBoost = Mathf.Lerp(0f, sunriseSunsetSaturationBoost, Mathf.InverseLerp(150f, 165f, angle));
        else if (angle >= 165f && angle < 180f)
            saturationBoost = Mathf.Lerp(sunriseSunsetSaturationBoost, 0f, Mathf.InverseLerp(165f, 180f, angle));

        Color.RGBToHSV(baseColor, out float h, out float s, out float v);
        s = Mathf.Clamp01(s + saturationBoost);
        return Color.HSVToRGB(h, s, v);
    }

    private float CalculateExposureFromAngle(float angle)
    {
        if (angle < 15f)
            return Mathf.Lerp(minimumNightExposure, maximumDayExposure, Mathf.InverseLerp(0f, 15f, angle));
        else if (angle < 165f)
            return maximumDayExposure;
        else if (angle < 180f)
            return Mathf.Lerp(maximumDayExposure, minimumNightExposure, Mathf.InverseLerp(165f, 180f, angle));
        else
            return minimumNightExposure;
    }

    [PunRPC]
    private void SyncLightingToClients(float masterTime)
    {
        // Only update if we're not the master client (unless it's our own RPC)
        if (!PhotonNetwork.IsMasterClient || masterTime == timeOfDay)
        {
            timeOfDay = masterTime;
            lastSentTimeOfDay = timeOfDay;
            ReapplySkybox();
        }
    }

    public void SetTimeOfDay(float newTime, bool sync = true)
    {
        timeOfDay = Mathf.Clamp01(newTime);
        if (sync && PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("SyncLightingToClients", RpcTarget.All, timeOfDay);
        }
    }

    public void PauseTime(bool paused)
    {
        isTimePaused = paused;
    }

    public void ToggleTimePause()
    {
        isTimePaused = !isTimePaused;
    }

    private void ReapplySkybox()
    {
        // Load material if needed
        if (skyboxMaterial == null)
            skyboxMaterial = Resources.Load<Material>("HawkProcedural");

        // Apply only if not already set
        if (RenderSettings.skybox != skyboxMaterial)
        {
            RenderSettings.skybox = skyboxMaterial;
            DynamicGI.UpdateEnvironment();
        }

        // Clear per-camera override
        var cam = Camera.main;
        if (cam != null)
        {
            Skybox sb = cam.GetComponent<Skybox>();
            if (sb != null && sb.material != null)
                sb.material = null;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting && PhotonNetwork.IsMasterClient)
        {
            stream.SendNext(timeOfDay);
            stream.SendNext(isTimePaused);
        }
        else
        {
            timeOfDay = (float)stream.ReceiveNext();
            isTimePaused = (bool)stream.ReceiveNext();
        }
    }
}