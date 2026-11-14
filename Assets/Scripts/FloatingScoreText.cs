using TMPro;
using UnityEngine;

/// World-space "+10" / "-5" that pops up where a point block landed, faces the player and fades out.
/// Gained points rise; lost points shake and then sink.
public class FloatingScoreText : MonoBehaviour
{
    private TextMeshPro text;
    private Vector3 origin;
    private float blockLength;
    private float baseScale;
    private float duration;
    private bool positive;
    private float age;
    private Transform viewer;

    public static void Create(Vector3 position, int points, float blockLength, PointEffectSettings settings)
    {
        var go = new GameObject(points >= 0 ? "Score Popup +" : "Score Popup -");
        go.transform.position = position;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = points >= 0 ? "+" + points : points.ToString();
        tmp.fontSize = 10f; // 1 world unit per em before scaling
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);
        tmp.color = points >= 0 ? settings.positiveNumberColor : settings.negativeNumberColor;
        tmp.outlineColor = settings.numberOutlineColor;
        tmp.outlineWidth = 0.25f;

        var popup = go.AddComponent<FloatingScoreText>();
        popup.text = tmp;
        popup.origin = position;
        popup.blockLength = blockLength;
        popup.duration = settings.numberDuration;
        popup.positive = points >= 0;
        // Digit height ~0.7 em, so this makes the digits numberHeight * blockLength tall.
        popup.baseScale = settings.numberHeight * blockLength / 0.7f;
        popup.Tick(0f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= duration)
        {
            Destroy(gameObject);
            return;
        }
        Tick(age);
    }

    private void Tick(float t)
    {
        if (viewer == null && Camera.main != null)
            viewer = Camera.main.transform;

        float n = t / duration;
        Vector3 right = viewer != null ? viewer.right : Vector3.right;
        Vector3 offset;
        float scale;

        if (positive)
        {
            // Pop with overshoot, then float upwards and slow down.
            scale = Pop(t, 1.3f);
            float rise = 1f - (1f - n) * (1f - n);
            offset = Vector3.up * (rise * 0.9f * blockLength);
        }
        else
        {
            // Smaller pop, a short angry shake, then sink away.
            scale = Pop(t, 1.1f);
            const float shakeTime = 0.45f;
            float shake = t < shakeTime ? Mathf.Sin(t * 55f) * (1f - t / shakeTime) * 0.08f * blockLength : 0f;
            float sink = t > shakeTime ? Mathf.Pow((t - shakeTime) / (duration - shakeTime), 2f) : 0f;
            offset = right * shake + Vector3.down * (sink * 0.7f * blockLength);
        }

        transform.position = origin + offset;
        transform.localScale = Vector3.one * (baseScale * scale);

        if (viewer != null)
            transform.rotation = Quaternion.LookRotation(transform.position - viewer.position, Vector3.up);

        const float fadeStart = 0.6f;
        text.alpha = n < fadeStart ? 1f : 1f - (n - fadeStart) / (1f - fadeStart);
    }

    private static float Pop(float t, float overshoot)
    {
        const float growTime = 0.15f, settleTime = 0.3f;
        if (t < growTime)
            return Mathf.SmoothStep(0f, overshoot, t / growTime);
        if (t < settleTime)
            return Mathf.Lerp(overshoot, 1f, (t - growTime) / (settleTime - growTime));
        return 1f;
    }
}
