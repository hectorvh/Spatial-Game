using TMPro;
using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        transform.rotation = cam.transform.rotation;
    }
}

public static class QuizText
{
    public static TextMeshPro Create(Transform parent, string text, Vector3 localPosition, float fontSize, Vector2 size, bool faceCamera = true)
    {
        GameObject labelObject = new GameObject("QuizLabel");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = localPosition;
        labelObject.transform.localRotation = Quaternion.identity;
        labelObject.transform.localScale = Vector3.one;

        TextMeshPro tmp = labelObject.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.rectTransform.sizeDelta = size;
        tmp.sortingOrder = 10;

        if (faceCamera)
        {
            labelObject.AddComponent<FaceCamera>();
        }

        return tmp;
    }

    public static TextMeshPro CreateOnBoxFace(Transform box, string text)
    {
        Vector3 boxScale = box.localScale;
        Vector3 labelScale = new Vector3(
            1f / Mathf.Max(boxScale.x, 0.01f),
            1f / Mathf.Max(boxScale.y, 0.01f),
            1f / Mathf.Max(boxScale.z, 0.01f)
        );

        // Keep the rect inside the painted face so neighboring lane labels cannot overlap.
        Vector2 faceSize = new Vector2(boxScale.x * 0.86f, boxScale.y * 0.78f);

        TextMeshPro tmp = Create(
            box,
            text,
            new Vector3(0f, 0f, -0.52f),
            3.2f,
            faceSize,
            false
        );
        tmp.transform.localScale = labelScale;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1.4f;
        tmp.fontSizeMax = 3.6f;
        tmp.margin = new Vector4(0.06f, 0.04f, 0.06f, 0.04f);
        tmp.lineSpacing = -8f;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.enableWordWrapping = true;
        tmp.alignment = TextAlignmentOptions.Midline;
        return tmp;
    }
}
