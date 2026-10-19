using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class QuestionDatabase
{
    readonly List<Question> questions = new List<Question>();
    int nextIndex;

    public bool IsLoaded => questions.Count > 0;
    public bool HasRemaining => nextIndex < questions.Count;

    public IEnumerator Load(string fileName)
    {
        questions.Clear();
        nextIndex = 0;

        string filePath = Application.streamingAssetsPath + "/" + fileName;
        string json = null;

        if (filePath.Contains("://") || filePath.Contains(":///"))
        {
            using (UnityWebRequest request = UnityWebRequest.Get(filePath))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Failed to load questions from " + filePath + ": " + request.error);
                    yield break;
                }

                json = request.downloadHandler.text;
            }
        }
        else
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError("Questions file not found: " + filePath);
                yield break;
            }

            json = File.ReadAllText(filePath);
        }

        QuestionList list = JsonUtility.FromJson<QuestionList>(json);
        if (list == null || list.questions == null || list.questions.Length == 0)
        {
            Debug.LogError("Questions JSON is empty or invalid: " + filePath);
            yield break;
        }

        questions.AddRange(list.questions);
        Shuffle(questions);
        Debug.Log("Loaded " + questions.Count + " quiz questions.");
    }

    public Question GetNext()
    {
        if (nextIndex >= questions.Count)
        {
            return null;
        }

        return questions[nextIndex++];
    }

    static void Shuffle(List<Question> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Question temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
