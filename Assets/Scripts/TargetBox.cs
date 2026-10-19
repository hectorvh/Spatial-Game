using System.Collections;
using UnityEngine;

public class TargetBox : MonoBehaviour
{
    [Header("Box Settings")]
    public bool isDestructible = false;
    public string answerText;
    public int questionId;
    public string questionText;
    public GameObject explosionEffect;

    [Header("Movement Settings")]
    public float moveSpeed = 10.0f;
    private PlayerControllerX playerScript;
    private QuestionWave wave;
    bool isAccepting;

    void Start()
    {
        playerScript = FindAnyObjectByType<PlayerControllerX>();
        wave = GetComponentInParent<QuestionWave>();
    }

    public void Setup(string displayedAnswer, bool isCorrect, int quizQuestionId, string quizQuestion)
    {
        answerText = displayedAnswer;
        isDestructible = isCorrect;
        questionId = quizQuestionId;
        questionText = quizQuestion;
        wave = GetComponentInParent<QuestionWave>();
        QuizText.CreateOnBoxFace(transform, answerText);
    }

    void Update()
    {
        if (playerScript != null && playerScript.isGameOver) return;

        if (wave != null)
        {
            return;
        }

        transform.Translate(Vector3.back * moveSpeed * Time.deltaTime, Space.World);

        if (Mathf.Abs(transform.position.z) > 60.0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Bullet"))
        {
            return;
        }

        Destroy(other.gameObject);

        if (isAccepting)
        {
            return;
        }

        if (isDestructible)
        {
            isAccepting = true;

            Collider boxCollider = GetComponent<Collider>();
            if (boxCollider != null)
            {
                boxCollider.enabled = false;
            }

            PlaySessionLogger.Ensure().LogShotCorrect(CurrentLane(), questionId, questionText, answerText);
            AudioManager.PlayCorrectHit();

            Vector3 visiblePosition = CurvedWorld.Bend(transform.position);
            GameHUD.Ensure().PlayCorrectScoreCollect(visiblePosition);

            if (explosionEffect != null)
            {
                Instantiate(explosionEffect, visiblePosition, transform.rotation);
            }

            if (wave != null)
            {
                wave.ResolveCorrect(transform.position);
            }
            else
            {
                StartCoroutine(SoloAcceptRoutine());
            }
        }
        else
        {
            PlaySessionLogger.Ensure().LogShotIncorrect(CurrentLane(), questionId, questionText, answerText);
            AudioManager.PlayWrongShot();
            Debug.Log("Incorrect box: " + answerText);
        }
    }

    IEnumerator SoloAcceptRoutine()
    {
        yield return PickupEffect.ScaleOut(transform);
        Destroy(gameObject);
    }

    int CurrentLane()
    {
        return playerScript != null ? playerScript.currentLane : -1;
    }
}
