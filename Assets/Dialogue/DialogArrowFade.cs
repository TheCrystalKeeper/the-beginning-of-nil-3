using UnityEngine;
using UnityEngine.UI;

public class DialogueArrowFade : MonoBehaviour
{
    [Header("References")]
    public GameObject target;
    public Text textElement;

    [Header("Position/Offset")]
    public float offsetX;
    public float offsetY;

    [Header("Dialogue")]
    public int dialogueID;
    [TextArea(10, 20)]
    public string content;

    [Header("Fade Settings")]
    public float fadeStartDistance = 4f;
    public float fadeFullDistance = 1.5f;

    private SpriteRenderer rend;
    private float originalX;
    private float originalY;
    private float alphavalue;
    private float textAlpha;
    private int item = -1;
    private int textLength;
    private bool textState = false;

    void Start()
    {
        originalX = transform.position.x;
        originalY = transform.position.y;

        rend = GetComponent<SpriteRenderer>();
        if (rend != null)
        {
            var c = rend.material.color;
            c.a = 0f;
            rend.material.color = c;
        }

        if (textElement != null)
            textElement.CrossFadeAlpha(0, 0f, false);

        GetLength(content, ref textLength);
    }

    void Update()
    {
        if (target != null)
        {
            Vector3 focus = new Vector3(originalX + offsetX, originalY + offsetY, transform.position.z);
            float dist = Vector3.Distance(focus, target.transform.position);

            if (dist > fadeStartDistance) alphavalue = 0f;
            else if (dist < fadeFullDistance) alphavalue = 1f;
            else alphavalue = 1f - ((dist - fadeFullDistance) / (fadeStartDistance - fadeFullDistance));
        }
        else
        {
            alphavalue = 1f;
        }

        SetText();

        if (rend != null) SetAlpha(ref rend, alphavalue);

        transform.position = new Vector3(
            originalX,
            originalY + Mathf.Cos(2f * Time.time) / 15f,
            transform.position.z
        );
    }

    void SetAlpha(ref SpriteRenderer sr, float value)
    {
        var c = sr.material.color;
        if (!Mathf.Approximately(c.a, value))
        {
            c.a = value;
            sr.material.color = c;
        }
    }

    void SetText()
    {
        if (textElement == null) return;

        if (alphavalue >= 0.5f)
        {
            textElement.GetComponent<CanvasRenderer>().SetAlpha(textAlpha);

            if (Input.GetKeyDown(KeyCode.X))
            {
                item += 1;
                textState = false;
            }
            else if (item > -1)
            {
                if (Mathf.Approximately(textAlpha, 0f))
                {
                    textState = true;
                    int nextIndex = item * 2;
                    if (nextIndex < textLength)
                        textElement.text = GetLine(content, nextIndex);
                    else
                    {
                        item = -1;
                        textState = false;
                        textElement.text = string.Empty;
                    }
                }
            }
        }

        if (textState) textAlpha += 0.07f;
        else textAlpha -= 0.06f;

        textAlpha = Mathf.Clamp01(textAlpha);
    }

    static string GetLine(string input, int lineWanted)
    {
        var lines = input.Split('\n');
        return (lineWanted >= 0 && lineWanted < lines.Length) ? lines[lineWanted] : string.Empty;
    }

    static void GetLength(string input, ref int textLength)
    {
        var lines = input.Split('\n');
        textLength = lines.Length;
    }
}
