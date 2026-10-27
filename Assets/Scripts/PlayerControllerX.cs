using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerControllerX : MonoBehaviour
{
    private Rigidbody playerRb;
    private Animator playerAnim;

    [Header("3-Rail System Settings")]
    public float centerRailX = -2.10f;
    public float laneSpacing = 3.0f;
    public float[] railsX = new float[] { -5.10f, -2.10f, 0.90f };
    public int currentLane = 1; 
    public float laneChangeSpeed = 22.0f; 

    public bool isGameOver = false;

    /// <summary>Hangar approach: steering and shooting are off and the bunny holds the center rail.</summary>
    public bool ControlLocked { get; private set; }
    bool scriptedMotion;

    [Header("Damage Settings")]
    public float invulnerabilityDuration = 1.5f;
    bool isInvulnerable;
    Quaternion startRotation;

    [Header("Lane Banking")]
    [Tooltip("Degrees of roll per m/s of sideways speed.")]
    public float bankFactor = 1.2f;
    public float maxBankAngle = 18f;
    [Tooltip("Degrees of yaw toward the target lane per m/s of sideways speed.")]
    public float turnFactor = 0.8f;
    public float maxYawAngle = 12f;
    [Tooltip("Lower is snappier; higher gives more weight and a slower settle.")]
    public float bankSmoothTime = 0.08f;
    float lateralSpeed;
    float bankAngle;
    float bankVelocity;
    float yawAngle;
    float yawVelocity;

    const RigidbodyConstraints AliveConstraints =
        RigidbodyConstraints.FreezeRotation |
        RigidbodyConstraints.FreezePositionX |
        RigidbodyConstraints.FreezePositionY |
        RigidbodyConstraints.FreezePositionZ;

    [Header("Shooting Settings")]
    public GameObject laserPrefab; 
    public Transform cannon; // Un solo cañón 

    [Header("Input Actions")]
    private InputAction moveLeftAction;
    private InputAction moveRightAction;
    private InputAction shootAction;

    float startY;
    bool touchTracking;
    bool touchDraggedLane;
    Vector2 touchOrigin;
    readonly List<RaycastResult> uiHits = new List<RaycastResult>(); 

    void Awake()
    {
        RebuildRails();
    }

    void RebuildRails()
    {
        railsX = new float[]
        {
            centerRailX - laneSpacing,
            centerRailX,
            centerRailX + laneSpacing
        };
    }

    void OnValidate()
    {
        RebuildRails();
    }

    void Start()
    {
        playerRb = GetComponent<Rigidbody>();
        playerAnim = GetComponent<Animator>();
        
        if (playerAnim != null) playerAnim.applyRootMotion = false;

        // Locked so bumping into boxes cannot push or spin the character.
        // Position and rotation stay as placed in the scene; only X changes when changing lanes.
        playerRb.constraints = AliveConstraints;
        startRotation = transform.rotation;
        startY = transform.position.y;

        PlaySessionLogger.Ensure();
        GameHUD.Ensure();
        CameraLaneFollow.Ensure();
        AudioManager.Ensure();
        AmbientDebrisManager.Ensure();
        CurvedWorldController.Ensure();
        LevelFlowController.Ensure();

        // Left Rail Action 
        moveLeftAction = new InputAction("MoveLeft");
        moveLeftAction.AddBinding("<Keyboard>/leftArrow");
        moveLeftAction.AddBinding("<Keyboard>/a");
        moveLeftAction.Enable();

        // Right Rail Action 
        moveRightAction = new InputAction("MoveRight");
        moveRightAction.AddBinding("<Keyboard>/rightArrow");
        moveRightAction.AddBinding("<Keyboard>/d");
        moveRightAction.Enable();

        // Shoot Action
        shootAction = new InputAction("Shoot", binding: "<Keyboard>/space");
        shootAction.Enable();
    }

    private void OnDestroy()
    {
        moveLeftAction?.Disable();
        moveRightAction?.Disable();
        shootAction?.Disable();
    }

    void Update()
    {
        if (!isGameOver)
        {
            if (GameHUD.Instance != null && GameHUD.Instance.IsPaused)
            {
                return;
            }

            if (scriptedMotion)
            {
                return;
            }

            bool acceptInput = !ControlLocked && LevelFlowController.InputEnabled;

            if (acceptInput)
            {
                HandleGameplayTouch();
            }
            else
            {
                touchTracking = false;
            }

            // Change lane
            if (acceptInput && moveLeftAction.triggered)
            {
                ChangeLane(-1);
            }
            else if (acceptInput && moveRightAction.triggered)
            {
                ChangeLane(1);
            }

            // Smooth sliding towards the target lane
            float targetX = railsX[currentLane];
            Vector3 currentPos = transform.position;
            float previousX = currentPos.x;
            currentPos.x = Mathf.MoveTowards(currentPos.x, targetX, laneChangeSpeed * Time.deltaTime);
            currentPos.y = startY;
            transform.position = currentPos;
            lateralSpeed = Time.deltaTime > 0f ? (currentPos.x - previousX) / Time.deltaTime : 0f;

            // Shoot Blaster 
            if (acceptInput && shootAction.triggered)
            {
                ShootBlaster();
            }
        }
    }

    // Applied after the Animator so the run clip cannot overwrite the lean.
    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (isGameOver || scriptedMotion || dt <= 0f)
        {
            return;
        }

        float targetRoll = Mathf.Clamp(-lateralSpeed * bankFactor, -maxBankAngle, maxBankAngle);
        float targetYaw = Mathf.Clamp(lateralSpeed * turnFactor, -maxYawAngle, maxYawAngle);
        bankAngle = Mathf.SmoothDamp(bankAngle, targetRoll, ref bankVelocity, bankSmoothTime, Mathf.Infinity, dt);
        yawAngle = Mathf.SmoothDamp(yawAngle, targetYaw, ref yawVelocity, bankSmoothTime, Mathf.Infinity, dt);

        // World-space yaw and roll on top of the placed pose, whatever way the model was authored.
        transform.rotation = Quaternion.Euler(0f, yawAngle, bankAngle) * startRotation;
    }

    void ResetBanking()
    {
        lateralSpeed = 0f;
        bankAngle = 0f;
        bankVelocity = 0f;
        yawAngle = 0f;
        yawVelocity = 0f;
    }

    void ChangeLane(int direction)
    {
        int next = currentLane + direction;
        if (next < 0 || next >= railsX.Length)
        {
            return;
        }

        int fromLane = currentLane;
        currentLane = next;
        PlaySessionLogger.Ensure().LogLaneChange(fromLane, currentLane);
        AudioManager.PlayLaneShift();
    }

    /// <summary>
    /// One finger on the phone. A sideways drag changes lane once. A press and release
    /// that never crosses that drag is a single shot. The two never both happen.
    /// </summary>
    void HandleGameplayTouch()
    {
        Touchscreen screen = Touchscreen.current;
        if (screen == null)
        {
            touchTracking = false;
            return;
        }

        UnityEngine.InputSystem.Controls.TouchControl touch = screen.primaryTouch;
        if (touch.press.wasPressedThisFrame)
        {
            Vector2 position = touch.position.ReadValue();
            touchTracking = !TouchBlockedByUi(position);
            touchDraggedLane = false;
            touchOrigin = position;
        }

        if (!touchTracking)
        {
            return;
        }

        Vector2 now = touch.position.ReadValue();
        Vector2 travel = now - touchOrigin;
        float laneDistance = LaneSwipeDistance();
        if (!touchDraggedLane && Mathf.Abs(travel.x) >= laneDistance && Mathf.Abs(travel.x) > Mathf.Abs(travel.y))
        {
            touchDraggedLane = true;
            ChangeLane(travel.x < 0f ? -1 : 1);
        }

        if (touch.press.wasReleasedThisFrame)
        {
            touchTracking = false;
            if (!touchDraggedLane)
            {
                ShootBlaster();
            }
        }
    }

    /// <summary>Above a normal tap wobble, below a long swipe. About 0.22 inch.</summary>
    static float LaneSwipeDistance()
    {
        float fromDpi = Screen.dpi > 1f ? Screen.dpi * 0.22f : 64f;
        float fromScreen = Mathf.Max(1f, Screen.width) * 0.07f;
        return Mathf.Clamp(Mathf.Min(fromDpi, fromScreen), 36f, 110f);
    }

    bool TouchBlockedByUi(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        uiHits.Clear();
        PointerEventData data = new PointerEventData(EventSystem.current) { position = screenPosition };
        EventSystem.current.RaycastAll(data, uiHits);
        return uiHits.Count > 0;
    }

    /// <summary>Hangar threshold: input stops and the bunny slides back to the center rail.</summary>
    public void LockControl()
    {
        ControlLocked = true;
        currentLane = railsX.Length / 2;
    }

    /// <summary>Boarding cutscene. LevelFlowController moves the transform from here on.</summary>
    public void BeginScriptedMotion()
    {
        LockControl();
        scriptedMotion = true;
        ResetBanking();
        transform.rotation = startRotation;
    }

    private void ShootBlaster()
    {
        if (laserPrefab != null && cannon != null)
        {
            GameObject shot = Instantiate(laserPrefab, cannon.position, laserPrefab.transform.rotation);
            CurvedWorld.Apply(shot);
            PlaySessionLogger.Ensure().LogShotFired(currentLane);
            RunStats.RecordShot();
            AudioManager.PlayShoot();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isGameOver || other == null || !other.CompareTag(SpatialStimulusToken.StimulusTag))
        {
            return;
        }

        SpatialStimulusToken token = other.GetComponent<SpatialStimulusToken>();
        if (token == null)
        {
            token = other.GetComponentInParent<SpatialStimulusToken>();
        }

        token?.TryCollect(this);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(SpatialStimulusToken.StimulusTag) ||
            collision.gameObject.CompareTag("Pickup"))
        {
            return;
        }

        if (!collision.gameObject.CompareTag("Obstacle"))
        {
            return;
        }

        if (isGameOver || isInvulnerable)
        {
            return;
        }

        collision.transform.SetParent(null, true);
        GameHUD.Ensure().ApplyCollisionPenalty();
        PlaySessionLogger.Ensure().LogCrash(currentLane, collision.gameObject.name);
        RunStats.RecordCrash();
        AudioManager.PlayCrash();
        KnockAway(collision.gameObject);
        HoldPositionSteady();
        StartCoroutine(InvulnerabilityRoutine());
        if (collision.gameObject.GetComponent<TargetBox>() != null ||
            collision.gameObject.GetComponentInParent<QuestionWave>() != null)
        {
            FindAnyObjectByType<BoxSpawner>()?.NotifyLastQuestionResolved();
        }
        Debug.Log("Collision: score -50. Score now: " + GameHUD.Ensure().score);
    }

    public void EndRun()
    {
        EndRun(true);
    }

    /// <param name="showFinishPanel">False when the level-clear screen replaces the FINISHED panel.</param>
    public void EndRun(bool showFinishPanel)
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;
        HoldPositionSteady();

        if (playerAnim != null)
        {
            playerAnim.enabled = false;
        }

        moveLeftAction?.Disable();
        moveRightAction?.Disable();
        shootAction?.Disable();

        GameHUD.Ensure().ShowFinish(showFinishPanel);
        PlaySessionLogger.Ensure().SaveSession();
        Debug.Log("Quiz finished. Final score: " + GameHUD.Ensure().score);
    }

    void KnockAway(GameObject obstacle)
    {
        Collider obstacleCollider = obstacle.GetComponent<Collider>();
        if (obstacleCollider != null)
        {
            // Stops the flying box from colliding with the player a second time.
            obstacleCollider.isTrigger = true;
        }

        Rigidbody obstacleRb = obstacle.GetComponent<Rigidbody>();
        if (obstacleRb != null)
        {
            obstacleRb.isKinematic = false;
            obstacleRb.AddForce(new Vector3(0, 15, 20), ForceMode.Impulse);
            obstacleRb.AddTorque(new Vector3(Random.Range(-50, 50), Random.Range(-50, 50), Random.Range(-50, 50)), ForceMode.Impulse);
        }

        Destroy(obstacle, 1.5f);
    }

    void HoldPositionSteady()
    {
        playerRb.constraints = AliveConstraints;
        playerRb.linearVelocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;
        ResetBanking();
        transform.rotation = startRotation;

        Vector3 pos = transform.position;
        pos.y = startY;
        transform.position = pos;
    }

    IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        const float blinkInterval = 0.12f;
        float elapsed = 0f;

        while (elapsed < invulnerabilityDuration)
        {
            bool visible = Mathf.FloorToInt(elapsed / blinkInterval) % 2 == 0;
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        isInvulnerable = false;
    }
}