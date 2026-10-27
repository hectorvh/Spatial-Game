using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxSpawner : MonoBehaviour
{
    [Header("Legacy box prefabs (kept in Assets, not spawned)")]
    public GameObject boxYes;
    public GameObject boxNo;
    public GameObject boxSkip;

    [Header("Answer Box")]
    public Vector3 answerBoxScale = new Vector3(2.6f, 1.7f, 1.2f);

    [Tooltip("Green / blue / purple taken from the game's neon green blaster and the HUD blue.")]
    public Color[] answerBoxColors =
    {
        new Color(0.153f, 0.706f, 0.286f, 1f),
        new Color(0.208f, 0.545f, 0.824f, 1f),
        new Color(0.514f, 0.341f, 0.824f, 1f)
    };

    [Tooltip("Optional override. Leave empty to use the generated sci-fi panel texture.")]
    public Texture answerBoxTexture;

    Material[] answerBoxMaterials;
    static Texture2D panelTexture;

    [Header("Game Mode")]
    public GameMode gameMode = GameMode.Trial;

    [Header("Spawn Settings")]
    // Valores exactos de tu imagen para los carriles
    public float[] laneXPositions = { -5.1f, -2.1f, 0.9f };
    public float spawnZ = 50.0f;

    [Header("Full-Span Stimulus (Phase 1)")]
    [Tooltip("Track center. The stimulus is locked here instead of picking a rail.")]
    public float stimulusCenterX = -2.1f;
    public float stimulusSpawnY = 3.37f;
    public float stimulusScale = 2.5f;

    [Tooltip("World-space trigger. X must span every rail so any lane intercepts it.")]
    public Vector3 stimulusTriggerSize = new Vector3(7.0f, 8.0f, 2.4f);

    [Tooltip("Offset from the stimulus root. Drops the trigger down to track level.")]
    public Vector3 stimulusTriggerCenter = new Vector3(0f, -2.4f, 0f);

    [Header("Timer Settings")]
    public float startDelay = 2.0f;
    public float spawnInterval = 3.0f;
    public float missRespawnDelay = 1.0f;
    public float answerWaveDelay = 1.5f;
    public float finishDelay = 4.0f;
    public float stimulusMoveSpeed = 10.0f;

    [Header("Questions")]
    public string questionsFileName = "questionsv2.json";

    enum TrialPhase
    {
        Idle,
        Stimulus,
        AwaitingAnswers,
        Answers
    }

    private PlayerControllerX playerScript;
    private readonly QuestionDatabase questionDatabase = new QuestionDatabase();
    TrialPhase phase = TrialPhase.Idle;
    Question activeQuestion;
    int lastStimulusLane = -1;
    int stimulusAttempt;
    bool stimulusResolved;
    bool stimulusAccepted;
    bool isFinishing;

    IEnumerator Start()
    {
        LevelFlowController.Ensure();
        GameModeSettings.CurrentMode = gameMode;
        playerScript = FindAnyObjectByType<PlayerControllerX>();
        if (playerScript != null && playerScript.railsX != null && playerScript.railsX.Length >= 3)
        {
            laneXPositions = playerScript.railsX;
        }
        yield return questionDatabase.Load(questionsFileName);

        if (!questionDatabase.IsLoaded)
        {
            Debug.LogError("BoxSpawner: no questions loaded. Waves will not spawn.");
            yield break;
        }

        // Level select, intro video and the mode gate run first.
        yield return new WaitUntil(() => LevelFlowController.RunStarted);
        yield return new WaitForSeconds(startDelay);
        yield return RunTrials();
    }

    IEnumerator RunTrials()
    {
        while (playerScript == null || !playerScript.isGameOver)
        {
            Question question = questionDatabase.GetNext();
            if (question == null)
            {
                TryFinishQuiz();
                yield break;
            }

            if (question.answers == null || question.answers.Length < 3)
            {
                Debug.LogWarning("BoxSpawner: skipped a question because the data is incomplete.");
                continue;
            }

            yield return RunStimulusPhase(question);
            if (playerScript != null && playerScript.isGameOver)
            {
                yield break;
            }

            yield return new WaitForSeconds(answerWaveDelay);
            if (playerScript != null && playerScript.isGameOver)
            {
                yield break;
            }

            SpawnAnswerWave(question);
            yield return new WaitUntil(IsAnswerPhaseClear);

            if (playerScript != null && playerScript.isGameOver)
            {
                yield break;
            }
        }
    }

    IEnumerator RunStimulusPhase(Question question)
    {
        activeQuestion = question;
        stimulusAttempt = 0;
        stimulusAccepted = false;
        lastStimulusLane = CenterLaneIndex();
        GameHUD.Ensure().ClearTrialBanner();

        while (!stimulusAccepted && (playerScript == null || !playerScript.isGameOver))
        {
            stimulusAttempt++;
            stimulusResolved = false;
            SpawnStimulusWave(question);
            yield return new WaitUntil(() => stimulusResolved || (playerScript != null && playerScript.isGameOver));

            if (stimulusAccepted || (playerScript != null && playerScript.isGameOver))
            {
                yield break;
            }

            yield return new WaitForSeconds(missRespawnDelay);
        }
    }

    /// <summary>Rail closest to the locked stimulus X, kept only so the log keeps a lane column.</summary>
    int CenterLaneIndex()
    {
        if (laneXPositions == null || laneXPositions.Length == 0)
        {
            return -1;
        }

        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < laneXPositions.Length; i++)
        {
            float distance = Mathf.Abs(laneXPositions[i] - stimulusCenterX);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    public void SpawnStimulusWave(Question question)
    {
        if (question == null)
        {
            stimulusResolved = true;
            return;
        }

        phase = TrialPhase.Stimulus;
        lastStimulusLane = CenterLaneIndex();

        GameObject tokenObject = new GameObject("QuestionStimulus");
        tokenObject.tag = SpatialStimulusToken.StimulusTag;
        tokenObject.transform.position = new Vector3(stimulusCenterX, stimulusSpawnY, spawnZ);
        tokenObject.transform.rotation = Quaternion.identity;

        BoxCollider trigger = tokenObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = stimulusTriggerSize;
        trigger.center = stimulusTriggerCenter;

        Rigidbody body = tokenObject.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // The model is scaled on a child so the trigger stays in world units.
        GameObject modelObject = new GameObject("StimulusModel");
        modelObject.transform.SetParent(tokenObject.transform, false);
        modelObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, stimulusScale);

        StimulusSceneBuilder builder = modelObject.AddComponent<StimulusSceneBuilder>();
        builder.Build(question);
        CurvedWorld.Apply(tokenObject);

        SpatialStimulusToken token = tokenObject.AddComponent<SpatialStimulusToken>();
        token.moveSpeed = stimulusMoveSpeed;
        token.Bind(this, question.id, lastStimulusLane, stimulusAttempt);

        PlaySessionLogger.Ensure().LogStimulusSpawned(question.id, lastStimulusLane, stimulusAttempt);
        Debug.Log("[Stimulus] Q" + question.id + " full-span attempt " + stimulusAttempt);
    }

    public void NotifyStimulusAccepted(SpatialStimulusToken token, float reactionTimeMs)
    {
        if (stimulusAccepted)
        {
            return;
        }

        stimulusAccepted = true;
        stimulusResolved = true;
        phase = TrialPhase.AwaitingAnswers;

        int lane = token != null ? token.laneIndex : lastStimulusLane;
        PlaySessionLogger.Ensure().LogStimulusAccepted(
            activeQuestion != null ? activeQuestion.id : -1,
            lane,
            stimulusAttempt,
            reactionTimeMs
        );
        RunStats.RecordStimulusReaction(reactionTimeMs);

        if (activeQuestion != null)
        {
            GameHUD.Ensure().OnQuestionAccepted(activeQuestion);
            PlaySessionLogger.Ensure().LogQuestionShown(activeQuestion.id, activeQuestion.question);
        }
    }

    public void NotifyStimulusMissed(SpatialStimulusToken token, int bunnyLane)
    {
        if (stimulusAccepted)
        {
            return;
        }

        stimulusResolved = true;
        int lane = token != null ? token.laneIndex : lastStimulusLane;
        PlaySessionLogger.Ensure().LogStimulusMissed(
            activeQuestion != null ? activeQuestion.id : -1,
            lane,
            stimulusAttempt,
            bunnyLane
        );
    }

    public void SpawnAnswerWave(Question question)
    {
        if (playerScript != null && playerScript.isGameOver)
        {
            return;
        }

        if (question == null || question.answers == null || question.answers.Length < 3)
        {
            Debug.LogWarning("BoxSpawner: cannot spawn answer wave because the question data is incomplete.");
            phase = TrialPhase.Idle;
            return;
        }

        phase = TrialPhase.Answers;

        List<float> availableLanes = new List<float>(laneXPositions);

        float centerX = 0f;
        for (int i = 0; i < laneXPositions.Length; i++)
        {
            centerX += laneXPositions[i];
        }
        centerX /= laneXPositions.Length;

        GameObject waveObject = new GameObject("QuestionWave");
        QuestionWave wave = waveObject.AddComponent<QuestionWave>();
        wave.questionText = question.question;
        waveObject.transform.position = new Vector3(centerX, wave.fallStartY, wave.approachZ);
        string correctAnswer = question.GetCorrectAnswer();
        GameHUD.Ensure().RegisterQuestionWave(wave);

        List<int> answerOrder = new List<int> { 0, 1, 2 };
        for (int i = answerOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = answerOrder[i];
            answerOrder[i] = answerOrder[j];
            answerOrder[j] = temp;
        }

        Debug.Log("[Quiz] " + question.question + " | correct answer: " + correctAnswer);

        List<int> colorOrder = BuildShuffledColorOrder();

        for (int i = 0; i < 3; i++)
        {
            int answerIndex = answerOrder[i];
            string answerText = question.answers[answerIndex];
            bool isCorrect = answerIndex == question.correctIndex;

            int randomLaneIndex = Random.Range(0, availableLanes.Count);
            float xPos = availableLanes[randomLaneIndex];
            availableLanes.RemoveAt(randomLaneIndex);

            Vector3 spawnPos = new Vector3(xPos, wave.fallStartY, wave.approachZ);
            GameObject spawnedBox = CreateSimpleAnswerBox(colorOrder[i]);
            spawnedBox.transform.SetParent(waveObject.transform, true);
            spawnedBox.transform.SetPositionAndRotation(spawnPos, Quaternion.identity);

            TargetBox boxScript = spawnedBox.GetComponent<TargetBox>();
            if (boxScript != null)
            {
                boxScript.Setup(answerText, isCorrect, question.id, question.question);
                wave.moveSpeed = boxScript.moveSpeed;
            }
        }

        CurvedWorld.Apply(waveObject);
        RunStats.RecordAnswerWave(question.id);
    }

    bool IsAnswerPhaseClear()
    {
        return phase != TrialPhase.Answers || (playerScript != null && playerScript.isGameOver);
    }

    public void NotifyWaveEnded()
    {
        NotifyWaveEnded(null);
    }

    public void NotifyWaveEnded(QuestionWave ended)
    {
        if (phase == TrialPhase.Answers)
        {
            phase = TrialPhase.Idle;
        }

        TryFinishQuiz(ended);
    }

    public void NotifyLastQuestionResolved()
    {
        if (isFinishing || (playerScript != null && playerScript.isGameOver))
        {
            return;
        }

        if (questionDatabase.HasRemaining)
        {
            return;
        }

        if (phase == TrialPhase.Stimulus || phase == TrialPhase.AwaitingAnswers)
        {
            return;
        }

        BeginFinishDelay();
    }

    void TryFinishQuiz()
    {
        TryFinishQuiz(null);
    }

    void TryFinishQuiz(QuestionWave ignore)
    {
        if (isFinishing || (playerScript != null && playerScript.isGameOver))
        {
            return;
        }

        if (questionDatabase.HasRemaining)
        {
            return;
        }

        if (phase == TrialPhase.Stimulus || phase == TrialPhase.AwaitingAnswers)
        {
            return;
        }

        QuestionWave[] waves = FindObjectsByType<QuestionWave>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int liveWaves = 0;
        for (int i = 0; i < waves.Length; i++)
        {
            if (waves[i] != null && waves[i] != ignore)
            {
                liveWaves++;
            }
        }

        if (liveWaves > 0)
        {
            return;
        }

        BeginFinishDelay();
    }

    void BeginFinishDelay()
    {
        if (isFinishing)
        {
            return;
        }

        isFinishing = true;
        StartCoroutine(FinishAfterDelay());
    }

    IEnumerator FinishAfterDelay()
    {
        yield return new WaitForSeconds(finishDelay);

        if (playerScript == null || playerScript.isGameOver)
        {
            yield break;
        }

        LevelFlowController flow = LevelFlowController.Instance;
        if (flow != null && flow.HandlesCorridorFinish)
        {
            flow.BeginCorridorFinish();
        }
        else
        {
            playerScript.EndRun();
        }
    }

    /// <summary>Random order over the palette so each box in a wave gets a different color.</summary>
    List<int> BuildShuffledColorOrder()
    {
        int paletteSize = answerBoxColors != null && answerBoxColors.Length > 0 ? answerBoxColors.Length : 1;

        List<int> order = new List<int>(paletteSize);
        for (int i = 0; i < paletteSize; i++)
        {
            order.Add(i);
        }

        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = order[i];
            order[i] = order[j];
            order[j] = temp;
        }

        while (order.Count < 3)
        {
            order.Add(order[order.Count % paletteSize]);
        }

        return order;
    }

    GameObject CreateSimpleAnswerBox(int colorIndex)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "AnswerBox";
        box.tag = "Obstacle";
        box.transform.localScale = answerBoxScale;

        Collider boxCollider = box.GetComponent<Collider>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = false;
        }

        Rigidbody body = box.AddComponent<Rigidbody>();
        body.mass = 10f;
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        Renderer boxRenderer = box.GetComponent<Renderer>();
        Material boxMaterial = GetAnswerBoxMaterial(colorIndex);
        if (boxRenderer != null && boxMaterial != null)
        {
            boxRenderer.sharedMaterial = boxMaterial;
        }

        box.AddComponent<TargetBox>();
        return box;
    }

    /// <summary>
    /// One shared material per palette entry: the crate panel texture tinted with the palette color.
    /// Shared so spawning waves does not leak a material per box.
    /// </summary>
    Material GetAnswerBoxMaterial(int colorIndex)
    {
        if (answerBoxColors == null || answerBoxColors.Length == 0)
        {
            return null;
        }

        colorIndex = Mathf.Clamp(colorIndex, 0, answerBoxColors.Length - 1);

        if (answerBoxMaterials == null || answerBoxMaterials.Length != answerBoxColors.Length)
        {
            answerBoxMaterials = new Material[answerBoxColors.Length];
        }

        if (answerBoxMaterials[colorIndex] != null)
        {
            return answerBoxMaterials[colorIndex];
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material material = new Material(shader != null ? shader : Shader.Find("Standard"));
        material.name = "AnswerBox_" + colorIndex;

        Texture texture = answerBoxTexture != null ? answerBoxTexture : GetPanelTexture();
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        material.mainTexture = texture;

        Color color = answerBoxColors[colorIndex];
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        material.color = color;

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.35f);
        }

        answerBoxMaterials[colorIndex] = material;
        return material;
    }

    /// <summary>
    /// Grayscale sci-fi panel matching the HUD look: dark inner field, bright border and corner bolts.
    /// Grayscale keeps the palette tint in full control of the hue.
    /// </summary>
    static Texture2D GetPanelTexture()
    {
        if (panelTexture != null)
        {
            return panelTexture;
        }

        const int size = 256;
        panelTexture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "AnswerBoxPanel",
            wrapMode = TextureWrapMode.Clamp
        };

        const float frameOuter = 12f;
        const float frameInner = 30f;
        const float boltInset = 46f;
        const float boltRadius = 7f;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance from the nearest edge drives the frame banding.
                float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));

                float luminance;
                if (edge < frameOuter)
                {
                    luminance = 0.42f;
                }
                else if (edge < frameInner)
                {
                    luminance = 1f;
                }
                else
                {
                    luminance = 0.34f;

                    // Faint horizontal plating lines across the inner field.
                    if ((y % 32) < 2)
                    {
                        luminance = 0.46f;
                    }
                }

                // Bolts in the four corners of the inner field.
                float bx = Mathf.Min(x, size - 1 - x) - boltInset;
                float by = Mathf.Min(y, size - 1 - y) - boltInset;
                if ((bx * bx) + (by * by) <= boltRadius * boltRadius)
                {
                    luminance = 0.95f;
                }

                pixels[(y * size) + x] = new Color(luminance, luminance, luminance, 1f);
            }
        }

        panelTexture.SetPixels(pixels);
        panelTexture.Apply();
        return panelTexture;
    }
}
