using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DialogueArrowFade : MonoBehaviour
{
    public enum InteractionMode { Dialogue, Event, EnterBuilding }

    [Header("Mode")]
    public InteractionMode mode = InteractionMode.Dialogue;

    [Header("Input")]
    public KeyCode interactKey = KeyCode.X;

    [Header("References")]
    public GameObject target;    // player
    public Text textElement;

    [Header("Position/Offset")]
    public float offsetX;
    public float offsetY;

    [Header("Dialogue")]
    public int dialogueID;
    [TextArea(10, 20)]
    public string content;

    [Header("Fade Settings (arrow visibility)")]
    public float fadeStartDistance = 4f;
    public float fadeFullDistance = 1.5f;
    [Range(0f, 1f)] public float interactAlphaThreshold = 0.5f;

    [Header("Enter Building")]
    public float enterFadeTime = 0.8f;
    public Color enterFadeColor = Color.black;
    public float waitTime = 0.5f;
    public Transform enterDestination;

    SpriteRenderer rend;
    float originalX, originalY;
    float alphavalue;
    float textAlpha;
    int item = -1;
    int textLength;
    bool textState = false;
    bool busy = false;
    bool dialogueActive = false;

    void Start()
    {
        originalX = transform.position.x;
        originalY = transform.position.y;

        rend = GetComponent<SpriteRenderer>();
        if (rend != null)
        {
            var c = rend.material.color; c.a = 0f; rend.material.color = c;
        }

        if (textElement != null) textElement.CrossFadeAlpha(0, 0f, false);

        GetLength(content, ref textLength);
    }

    void Update()
    {
        UpdateAlphaByDistance();

        bool canInteract = alphavalue >= interactAlphaThreshold && !busy && Input.GetKeyDown(interactKey);

        switch (mode)
        {
            case InteractionMode.Dialogue:
                HandleDialogue(canInteract);
                break;

            case InteractionMode.Event:
                if (canInteract)
                {
                    // placeholder for future event logic
                }
                break;

            case InteractionMode.EnterBuilding:
                if (canInteract) StartCoroutine(EnterBuildingRoutine());
                break;
        }

        if (rend != null) SetAlpha(ref rend, alphavalue);

        transform.position = new Vector3(
            originalX,
            originalY + Mathf.Cos(2f * Time.time) / 15f,
            transform.position.z
        );
    }

    void UpdateAlphaByDistance()
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
    }

    void HandleDialogue(bool interactPressed)
    {
        if (textElement == null) return;

        if (interactPressed && !dialogueActive)
        {
            dialogueActive = true;
            item = 0;
            textState = false;
        }

        if (interactPressed && dialogueActive)
        {
            item += 1;
            textState = false;
        }

        if (alphavalue >= interactAlphaThreshold || dialogueActive)
        {
            textElement.GetComponent<CanvasRenderer>().SetAlpha(textAlpha);

            if (item > -1)
            {
                if (Mathf.Approximately(textAlpha, 0f))
                {
                    textState = true;
                    int nextIndex = item * 2; // keep your even-line stepping
                    if (nextIndex < textLength)
                    {
                        textElement.text = GetLine(content, nextIndex);
                    }
                    else
                    {
                        item = -1;
                        textState = false;
                        textElement.text = string.Empty;
                        dialogueActive = false;
                    }
                }
            }
        }

        if (textState) textAlpha += 0.07f;
        else textAlpha -= 0.06f;
        textAlpha = Mathf.Clamp01(textAlpha);
    }

    IEnumerator EnterBuildingRoutine()
    {
        busy = true;

        var fm = Object.FindFirstObjectByType<FadeManager>();

        // Freeze player (if controller present)
        MinimalKinematicController2D ctrl = null;
        if (target != null)
            ctrl = target.GetComponent<MinimalKinematicController2D>();

        if (ctrl != null) ctrl.Freeze(zeroOutVelocity: true, clearInput: true);

        // Fade in
        if (fm != null) fm.FadeIn(enterFadeTime, enterFadeColor);
        while (FadeManager.IsFading) yield return null;

        // Move player
        if (target != null)
        {
            if (ctrl != null)
            {
                if (enterDestination != null)
                    ctrl.TeleportTo(enterDestination.position, recheckGroundNow: true);
            }
            else
            {
                // Fallback if controller not present
                if (enterDestination != null)
                    target.transform.position = enterDestination.position;
            }
        }

        // Unfreeze before fade out so input resumes after the cut
        if (ctrl != null) ctrl.Unfreeze();

        yield return new WaitForSeconds(waitTime);

        // Fade out
        if (fm != null) fm.FadeOut(enterFadeTime, enterFadeColor);
        while (FadeManager.IsFading) yield return null;

        busy = false;
    }

    void SetAlpha(ref SpriteRenderer sr, float value)
    {
        var c = sr.material.color;
        if (!Mathf.Approximately(c.a, value)) { c.a = value; sr.material.color = c; }
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
