using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Text;

[RequireComponent(typeof(PhotonView))]
public class ProximityChatText : MonoBehaviourPun
{
    [Header("Chat Settings")]
    public float messageDuration = 3f;
    public float fadeTime = 1f;

    [Header("Text Formatting")]
    public int maxCharactersPerLine = 20;
    public int maxLines = 3;
    public bool enableAutoScaling = true;
    public float minCharacterSize = 0.1f;
    public float maxCharacterSize = 0.2f;

    [Header("Billboard Settings")]
    public Camera referenceCamera;

    private TextMesh textMesh;
    private float fadeTimer = -1f;
    private Color baseColor;
    private float originalCharacterSize;

    private void Awake()
    {
        // Get the TextMesh component attached to this object
        textMesh = GetComponent<TextMesh>();

        if (textMesh == null)
        {
            Debug.LogError("ProximityChatText requires a TextMesh component on the same GameObject!");
            return;
        }

        // Initialize with empty text
        textMesh.text = "";
        baseColor = textMesh.color;
        originalCharacterSize = textMesh.characterSize;

        // Get camera reference
        if (referenceCamera == null)
            referenceCamera = Camera.main;
    }

    // Public function to be called from outside sources
    public void SetMessage(string newMessage)
    {
        if (textMesh == null || string.IsNullOrEmpty(newMessage)) return;

        Debug.Log($"Setting proximity chat message: '{newMessage}' (Length: {newMessage.Length})");
        Debug.Log($"Current settings - AutoScaling: {enableAutoScaling}, MaxLines: {maxLines}, MaxCharsPerLine: {maxCharactersPerLine}");

        // Format the message with line wrapping
        string formattedMessage = FormatMessage(newMessage);

        // Update locally
        ShowMessage(formattedMessage);

        // Sync with other players
        if (photonView.IsMine)
        {
            photonView.RPC("RPC_ShowMessage", RpcTarget.Others, newMessage);
        }
    }

    [PunRPC]
    private void RPC_ShowMessage(string message)
    {
        Debug.Log($"RPC received for proximity chat: {message}");
        string formattedMessage = FormatMessage(message);
        ShowMessage(formattedMessage);
    }

    private string FormatMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";

        // If auto-scaling is enabled, we'll use dynamic limits based on scale
        if (enableAutoScaling)
        {
            return FormatMessageWithAutoScaling(message);
        }
        else
        {
            return FormatMessageWithFixedLimits(message);
        }
    }

    private string FormatMessageWithFixedLimits(string message)
    {
        // Handle pre-formatted multi-line text
        if (message.Contains("\n"))
        {
            string[] lines = message.Split('\n');
            if (lines.Length > maxLines)
            {
                // Truncate to maxLines
                StringBuilder truncatedResult = new StringBuilder(); // Renamed to avoid conflict
                for (int i = 0; i < maxLines; i++)
                {
                    if (i > 0) truncatedResult.AppendLine();
                    truncatedResult.Append(lines[i]);
                }
                truncatedResult.Append("...");
                AdjustTextSize(1f);
                return truncatedResult.ToString();
            }
            else
            {
                AdjustTextSize(1f);
                return message; // Return original with preserved line breaks
            }
        }

        // Original logic for single-line text that needs wrapping
        if (message.Length <= maxCharactersPerLine)
        {
            AdjustTextSize(1f);
            return message;
        }

        string[] words = message.Split(' ');
        StringBuilder result = new StringBuilder();
        StringBuilder currentLine = new StringBuilder();
        int lineCount = 1;

        foreach (string word in words)
        {
            if (currentLine.Length + word.Length + 1 > maxCharactersPerLine)
            {
                if (lineCount >= maxLines)
                {
                    result.Append(currentLine.ToString().Trim());
                    result.Append("...");
                    break;
                }

                result.AppendLine(currentLine.ToString().Trim());
                currentLine.Clear();
                lineCount++;
            }

            currentLine.Append(word + " ");
        }

        if (lineCount <= maxLines && currentLine.Length > 0)
        {
            result.Append(currentLine.ToString().Trim());
        }

        AdjustTextSize(1f); // Fixed size
        return result.ToString();
    }

    private string FormatMessageWithAutoScaling(string message)
    {
        // If the message already has line breaks, we need to handle them differently
        if (message.Contains("\n"))
        {
            return FormatPreFormattedMessageWithAutoScaling(message);
        }

        // Start with base limits for regular text
        int currentMaxLines = maxLines;
        int currentMaxCharsPerLine = maxCharactersPerLine;
        float scaleFactor = 1f;

        // Keep scaling down until the text fits or we hit minimum size
        for (int i = 0; i < 10; i++) // Safety limit to prevent infinite loops
        {
            // Try to fit the message with current limits
            string formattedMessage = WrapText(message, currentMaxCharsPerLine, currentMaxLines, out int actualLineCount, out bool wasTruncated);

            // If text fits without truncation, we're done
            if (!wasTruncated)
            {
                AdjustTextSize(scaleFactor);
                return formattedMessage;
            }

            // Calculate new scale factor (reduce size)
            scaleFactor *= 0.8f; // Reduce by 20% each iteration
            scaleFactor = Mathf.Max(scaleFactor, minCharacterSize / originalCharacterSize);

            // Increase limits based on inverse scale
            currentMaxLines = Mathf.CeilToInt(maxLines / scaleFactor);
            currentMaxCharsPerLine = Mathf.CeilToInt(maxCharactersPerLine / scaleFactor);

            Debug.Log($"Auto-scaling iteration {i + 1}: scale={scaleFactor}, maxLines={currentMaxLines}, maxChars={currentMaxCharsPerLine}");
        }

        // If we get here, use the smallest possible size with maximum limits
        scaleFactor = minCharacterSize / originalCharacterSize;
        currentMaxLines = Mathf.CeilToInt(maxLines / scaleFactor);
        currentMaxCharsPerLine = Mathf.CeilToInt(maxCharactersPerLine / scaleFactor);

        string finalMessage = WrapText(message, currentMaxCharsPerLine, currentMaxLines, out int finalLineCount, out bool finalTruncated);
        AdjustTextSize(scaleFactor);

        Debug.Log($"Final auto-scaling: scale={scaleFactor}, lines={finalLineCount}, truncated={finalTruncated}");
        return finalMessage;
    }

    private string FormatPreFormattedMessageWithAutoScaling(string message)
    {
        // Split the pre-formatted message into lines
        string[] originalLines = message.Split('\n');
        int totalLines = originalLines.Length; // Use Length instead of Count()

        Debug.Log($"Processing pre-formatted message with {totalLines} lines");

        // If the message fits within maxLines at normal size, no scaling needed
        if (totalLines <= maxLines)
        {
            AdjustTextSize(1f);
            return message; // Return original with preserved line breaks
        }

        // Calculate required scale factor based on line count
        float scaleFactor = Mathf.Clamp((float)maxLines / totalLines, minCharacterSize / originalCharacterSize, 1f);

        // Apply scaling
        AdjustTextSize(scaleFactor);

        Debug.Log($"Pre-formatted scaling: {totalLines} lines -> scale={scaleFactor}");

        // Return original message with preserved formatting
        return message;
    }

    private string WrapText(string message, int charsPerLine, int maxLines, out int lineCount, out bool wasTruncated)
    {
        wasTruncated = false;
        lineCount = 1;

        // If message is short enough, return as is
        if (message.Length <= charsPerLine)
        {
            return message;
        }

        string[] words = message.Split(' ');
        StringBuilder result = new StringBuilder();
        StringBuilder currentLine = new StringBuilder();

        foreach (string word in words)
        {
            // Check if adding this word would exceed line length
            if (currentLine.Length + word.Length + 1 > charsPerLine)
            {
                if (lineCount >= maxLines)
                {
                    result.Append(currentLine.ToString().Trim());
                    result.Append("...");
                    wasTruncated = true;
                    break;
                }

                result.AppendLine(currentLine.ToString().Trim());
                currentLine.Clear();
                lineCount++;
            }

            currentLine.Append(word + " ");
        }

        if (!wasTruncated && currentLine.Length > 0)
        {
            result.Append(currentLine.ToString().Trim());
        }

        return result.ToString();
    }

    private void AdjustTextSize(float scaleFactor)
    {
        if (enableAutoScaling)
        {
            float newSize = originalCharacterSize * scaleFactor;
            textMesh.characterSize = Mathf.Clamp(newSize, minCharacterSize, maxCharacterSize);
        }
        else
        {
            textMesh.characterSize = originalCharacterSize;
        }
    }

    private void ShowMessage(string message)
    {
        textMesh.text = message;
        textMesh.color = baseColor;
        fadeTimer = fadeTime;

        Debug.Log($"TextMesh text set to: {textMesh.text}");
    }

    private void FixedUpdate()
    {
        if (textMesh == null) return;

        // Billboard effect - always face the camera
        if (referenceCamera == null)
            referenceCamera = Camera.main;

        if (referenceCamera != null)
        {
            // Make the text face the camera while maintaining up direction
            transform.rotation = Quaternion.LookRotation(transform.position - referenceCamera.transform.position);
        }

        // Handle fade out
        if (fadeTimer > 0f)
        {
            fadeTimer -= Time.fixedDeltaTime;

            if (fadeTimer <= fadeTime)
            {
                float alpha = Mathf.Clamp01(fadeTimer / fadeTime);
                textMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }

            if (fadeTimer <= 0f)
            {
                textMesh.text = "";
                // Reset character size when clearing text
                textMesh.characterSize = originalCharacterSize;
            }
        }
    }

    // Debug method to check component status
    private void Start()
    {
        Debug.Log($"ProximityChatText initialized - TextMesh: {textMesh != null}, PhotonView: {photonView != null}, IsMine: {photonView.IsMine}");
    }
}