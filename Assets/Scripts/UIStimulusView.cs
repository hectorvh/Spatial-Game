using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Upper-left HUD card that shows a shadowless copy of the accepted
/// question's gray box and red sphere via a dedicated render texture.
/// The preview uses the same perspective and rail-aligned angles as the
/// on-track stimulus; it does not follow the bunny across lanes.
/// </summary>
public class UIStimulusView : MonoBehaviour
{
    public const string PreviewLayerName = "UI_3D_Preview";

    const int TextureSize = 512;
    static readonly Color Backdrop = new Color(0.851f, 0.933f, 0.969f, 1f);
    const float DefaultTokenViewDistance = 16f;
    const float PreviewModelScale = 5f;

    RawImage rawImage;
    RenderTexture renderTexture;
    Camera previewCamera;
    Transform previewRoot;
    GameObject stage;
    int previewLayer;
    bool visible;
    bool sceneCamerasMasked;
    Vector3 cameraLocalPosition = new Vector3(0f, 5f, -16f);
    Quaternion cameraLocalRotation = Quaternion.Euler(17f, 0f, 0f);
    float cameraFieldOfView = 60f;

    public static UIStimulusView Attach(RectTransform panel, RawImage image)
    {
        UIStimulusView view = panel.GetComponent<UIStimulusView>();
        if (view == null)
        {
            view = panel.gameObject.AddComponent<UIStimulusView>();
        }

        view.rawImage = image;
        if (view.rawImage != null)
        {
            view.rawImage.raycastTarget = false;
        }

        view.Hide();
        return view;
    }

    public void Show(Question question)
    {
        if (question == null)
        {
            Hide();
            return;
        }

        EnsureStage();
        CaptureTrackView();
        if (previewCamera != null)
        {
            previewCamera.enabled = false;
        }

        RebuildPreview(question);
        LockCameraPose();
        ExcludePreviewLayerFromSceneCameras();

        visible = true;
        gameObject.SetActive(true);

        if (previewCamera != null)
        {
            previewCamera.enabled = true;
        }
    }

    public void Hide()
    {
        visible = false;
        if (previewCamera != null)
        {
            previewCamera.enabled = false;
        }

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public bool IsVisible => visible;

    void LateUpdate()
    {
        if (visible)
        {
            LockCameraPose();
        }
    }

    void EnsureStage()
    {
        previewLayer = LayerMask.NameToLayer(PreviewLayerName);
        if (previewLayer < 0)
        {
            previewLayer = 8;
        }

        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "StimulusRenderTexture",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            renderTexture.Create();
        }

        if (rawImage != null)
        {
            rawImage.texture = renderTexture;
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
        }

        if (stage != null)
        {
            LockCameraPose();
            return;
        }

        stage = new GameObject("UI_3D_Preview");
        DontDestroyOnLoad(stage);
        stage.transform.SetPositionAndRotation(new Vector3(0f, 5000f, 0f), Quaternion.identity);
        stage.transform.localScale = Vector3.one;

        previewRoot = new GameObject("StimulusPreviewRoot").transform;
        previewRoot.SetParent(stage.transform, false);
        previewRoot.localPosition = Vector3.zero;
        previewRoot.localRotation = Quaternion.identity;
        previewRoot.localScale = Vector3.one * PreviewModelScale;

        GameObject cameraObject = new GameObject("StimulusPreviewCamera");
        cameraObject.transform.SetParent(stage.transform, false);
        cameraObject.layer = 0;

        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.enabled = false;
        previewCamera.orthographic = false;
        previewCamera.fieldOfView = cameraFieldOfView;
        previewCamera.nearClipPlane = 0.3f;
        previewCamera.farClipPlane = 40f;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = Backdrop;
        previewCamera.cullingMask = 1 << previewLayer;
        previewCamera.targetTexture = renderTexture;
        previewCamera.allowHDR = false;
        previewCamera.allowMSAA = false;
        previewCamera.useOcclusionCulling = false;
        previewCamera.depth = -80;

        AudioListener listener = cameraObject.GetComponent<AudioListener>();
        if (listener != null)
        {
            Destroy(listener);
        }

        UniversalAdditionalCameraData cameraData = previewCamera.GetUniversalAdditionalCameraData();
        if (cameraData != null)
        {
            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.antialiasing = AntialiasingMode.None;
        }

        LockCameraPose();
        SetLayerRecursively(previewRoot.gameObject, previewLayer);
        ExcludePreviewLayerFromSceneCameras();
    }

    void CaptureTrackView()
    {
        Camera trackCamera = FindTrackCamera();
        float tokenY = 1f;
        BoxSpawner spawner = FindAnyObjectByType<BoxSpawner>();
        if (spawner != null)
        {
            tokenY = spawner.stimulusSpawnY;
        }

        if (trackCamera == null)
        {
            cameraFieldOfView = 60f;
            cameraLocalRotation = Quaternion.Euler(17f, 0f, 0f);
            cameraLocalPosition = new Vector3(0f, 6f - tokenY, -DefaultTokenViewDistance);
            ApplyCameraLens();
            return;
        }

        cameraFieldOfView = trackCamera.fieldOfView;

        Vector3 euler = trackCamera.transform.eulerAngles;
        // Same pitch/roll as the rail camera; yaw stays 0 so lane changes cannot swing the HUD.
        cameraLocalRotation = Quaternion.Euler(euler.x, 0f, euler.z);

        Vector3 camPos = trackCamera.transform.position;
        cameraLocalPosition = new Vector3(0f, camPos.y - tokenY, -DefaultTokenViewDistance);
        ApplyCameraLens();
    }

    void ApplyCameraLens()
    {
        if (previewCamera == null)
        {
            return;
        }

        previewCamera.orthographic = false;
        previewCamera.fieldOfView = cameraFieldOfView;
    }

    Camera FindTrackCamera()
    {
        if (CameraLaneFollow.Instance != null)
        {
            Camera followCamera = CameraLaneFollow.Instance.GetComponent<Camera>();
            if (followCamera != null && followCamera != previewCamera)
            {
                return followCamera;
            }
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera == previewCamera || camera.targetTexture != null)
            {
                continue;
            }

            return camera;
        }

        return null;
    }

    void LockCameraPose()
    {
        if (previewCamera == null)
        {
            return;
        }

        Transform cameraTransform = previewCamera.transform;
        cameraTransform.localPosition = cameraLocalPosition;
        cameraTransform.localRotation = cameraLocalRotation;
        cameraTransform.localScale = Vector3.one;

        if (previewRoot != null)
        {
            previewRoot.localPosition = Vector3.zero;
            previewRoot.localRotation = Quaternion.identity;
            previewRoot.localScale = Vector3.one * PreviewModelScale;
        }
    }

    void RebuildPreview(Question question)
    {
        if (previewRoot == null)
        {
            return;
        }

        ClearPreviewChildren();
        StimulusSceneBuilder.BuildHudPreview(previewRoot, question, previewLayer);
        SetLayerRecursively(previewRoot.gameObject, previewLayer);
        LockCameraPose();
    }

    void ClearPreviewChildren()
    {
        for (int i = previewRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = previewRoot.GetChild(i).gameObject;
            child.SetActive(false);
            child.transform.SetParent(null, false);
            Destroy(child);
        }
    }

    void ExcludePreviewLayerFromSceneCameras()
    {
        if (sceneCamerasMasked)
        {
            return;
        }

        int hideMask = ~(1 << previewLayer);
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera == previewCamera)
            {
                continue;
            }

            camera.cullingMask &= hideMask;
        }

        sceneCamerasMasked = true;
    }

    static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
        {
            return;
        }

        root.layer = layer;
        Transform transform = root.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            SetLayerRecursively(transform.GetChild(i).gameObject, layer);
        }
    }

    void OnDestroy()
    {
        if (previewCamera != null)
        {
            previewCamera.targetTexture = null;
            previewCamera.enabled = false;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }

        if (stage != null)
        {
            Destroy(stage);
            stage = null;
        }
    }
}
