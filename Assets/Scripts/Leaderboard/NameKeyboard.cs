using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Einfache VR-Tastatur zur Namenseingabe auf einem World-Space-Canvas (Bedienung per XR-Ray).
/// Die Buchstabentasten werden beim ersten Aktivieren aus <see cref="keyTemplate"/> erzeugt –
/// pro Zeile in <see cref="keyRows"/> eine Reihe im <see cref="keyContainer"/> (VerticalLayoutGroup).
///
/// Am einfachsten über GameObject > Bestenliste > Rundenende mit Tastatur anlegen.
/// </summary>
public class NameKeyboard : MonoBehaviour
{
    [Header("Referenzen")]
    [Tooltip("Vorlage für eine Taste (Button mit TMP-Text als Kind). Wird deaktiviert und kopiert.")]
    [SerializeField] Button keyTemplate;

    [Tooltip("Container für die Tastenreihen (mit VerticalLayoutGroup).")]
    [SerializeField] RectTransform keyContainer;

    [Tooltip("Anzeige des eingegebenen Namens.")]
    [SerializeField] TMP_Text nameText;

    [SerializeField] Button backspaceButton;
    [SerializeField] Button spaceButton;
    [SerializeField] Button confirmButton;
    [Tooltip("Optional.")]
    [SerializeField] Button cancelButton;

    [Header("Layout")]
    [Tooltip("Eine Zeile pro Tastenreihe.")]
    [SerializeField] string[] keyRows = { "1234567890", "QWERTZUIOPÜ", "ASDFGHJKLÖÄ", "YXCVBNM-" };

    [Tooltip("Abstand zwischen den Tasten einer Reihe.")]
    [SerializeField] float keySpacing = 8f;

    [Tooltip("Tastengröße (Breite x Höhe in Canvas-Einheiten). Mit 'Fit To Container' nur das Seitenverhältnis.")]
    [SerializeField] Vector2 keySize = new(80, 70);

    [Tooltip("Tasten so groß machen, dass die Reihen die Höhe des Containers ausfüllen " +
             "(die längste Reihe wird dabei nie breiter als der Container).")]
    [SerializeField] bool fitToContainer = true;

    [Tooltip("Schriftgröße relativ zur Tastenhöhe.")]
    [SerializeField, Range(0.2f, 0.9f)] float labelSizeFactor = 0.55f;

    [Header("Eingabe")]
    [SerializeField, Range(1, Leaderboard.MaxNameLength)] int maxLength = 12;

    [Tooltip("Erster Buchstabe (und nach Leerzeichen/Bindestrich) groß, Rest klein.")]
    [SerializeField] bool autoCapitalize = true;

    [SerializeField] string placeholder = "Enter name";

    [Header("Events")]
    public UnityEvent<string> onSubmitted;
    public UnityEvent onCancelled;

    readonly StringBuilder m_Name = new();
    bool m_Built;
    bool m_CaretVisible;

    public string CurrentName => m_Name.ToString().Trim();

    void Awake()
    {
        BuildKeys();

        if (backspaceButton != null) backspaceButton.onClick.AddListener(Backspace);
        if (spaceButton != null) spaceButton.onClick.AddListener(Space);
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
    }

    void OnEnable() => UpdateText();

    void Update()
    {
        // Blinkender Cursor – nur neu setzen, wenn er umschaltet.
        if (CaretVisible != m_CaretVisible)
            UpdateText();
    }

    static bool CaretVisible => Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;

    void BuildKeys()
    {
        if (m_Built || keyTemplate == null || keyContainer == null)
            return;
        m_Built = true;

        keyTemplate.gameObject.SetActive(false);
        Vector2 size = CalculateKeySize();

        foreach (string row in keyRows)
        {
            var rowObject = new GameObject("Reihe " + row, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowObject.transform.SetParent(keyContainer, false);

            var layout = rowObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = keySpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            foreach (char c in row)
            {
                Button key = Instantiate(keyTemplate, rowObject.transform);
                key.name = "Taste " + c;
                key.gameObject.SetActive(true);

                var label = key.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = c.ToString();
                    label.enableAutoSizing = false;
                    label.fontSize = size.y * labelSizeFactor;
                }

                var element = key.GetComponent<LayoutElement>();
                if (element == null)
                    element = key.gameObject.AddComponent<LayoutElement>();
                element.ignoreLayout = false;
                element.minWidth = element.preferredWidth = size.x;
                element.minHeight = element.preferredHeight = size.y;

                char character = c;
                key.onClick.AddListener(() => Type(character));
            }
        }
    }

    Vector2 CalculateKeySize()
    {
        if (!fitToContainer || keyRows.Length == 0)
            return keySize;

        Rect area = keyContainer.rect;
        float rowSpacing = 0f;
        if (keyContainer.TryGetComponent(out VerticalLayoutGroup vertical))
        {
            rowSpacing = vertical.spacing;
            area.height -= vertical.padding.vertical;
            area.width -= vertical.padding.horizontal;
        }

        int longestRow = 1;
        foreach (string row in keyRows)
            longestRow = Mathf.Max(longestRow, row.Length);

        // Höhe: alle Reihen füllen den Container aus.
        float height = (area.height - rowSpacing * (keyRows.Length - 1)) / keyRows.Length;
        // Breite: Seitenverhältnis aus keySize, aber die längste Reihe muss reinpassen.
        float aspect = keySize.y > 0f ? keySize.x / keySize.y : 1f;
        float maxWidth = (area.width - keySpacing * (longestRow - 1)) / longestRow;
        float width = Mathf.Min(height * aspect, maxWidth);

        if (height <= 1f || width <= 1f)
            return keySize; // Container hat (noch) keine sinnvolle Größe

        return new Vector2(width, height);
    }

    /// <summary>Leert die Eingabe (z.B. beim Öffnen).</summary>
    public void Clear()
    {
        m_Name.Clear();
        UpdateText();
    }

    public void Type(char c)
    {
        if (m_Name.Length >= maxLength)
            return;

        if (autoCapitalize && char.IsLetter(c))
        {
            bool wordStart = m_Name.Length == 0 || m_Name[^1] == ' ' || m_Name[^1] == '-';
            c = wordStart ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
        }

        m_Name.Append(c);
        UpdateText();
    }

    public void Space()
    {
        // Kein Leerzeichen am Anfang und keine doppelten.
        if (m_Name.Length == 0 || m_Name[^1] == ' ')
            return;
        Type(' ');
    }

    public void Backspace()
    {
        if (m_Name.Length > 0)
            m_Name.Length--;
        UpdateText();
    }

    public void Confirm() => onSubmitted?.Invoke(CurrentName);

    public void Cancel() => onCancelled?.Invoke();

    void UpdateText()
    {
        if (nameText == null)
            return;

        m_CaretVisible = CaretVisible;
        // TMP kennt kein </alpha> – die Transparenz wird einfach für den Cursor neu gesetzt.
        string caret = m_CaretVisible ? "<alpha=#AA>_" : "<alpha=#00>_";

        nameText.text = m_Name.Length == 0
            ? $"<alpha=#66>{placeholder}{caret}"
            : $"<noparse>{m_Name}</noparse>{caret}";
    }
}
