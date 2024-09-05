// Select how you would like to handle unreadable Texture2Ds by uncommenting one of the following #define statements:

// If a Texture2D has isReadable set to false then use the asset loader to
// locate the file within the Assets folder from which the Texture2D was loaded
// and read the pixel data directly from that file with System.IO.
// This is the recommended behaviour because it always works without errors, however
// it can be quite slow because loading the image from disk every time we want to
// regenerate a PixelCollider2D is not optimal.
#define OnUnreadableTexture_ReadFileDirectly

// If a Texture2D has isReadable set to false then throw a System.Exception
// to alert the user of the issue and allow them to fix it manually.
// This option is great if you want to fix each issue one at a time, however
// if you have hundreds of Texture2Ds this option can be time consuming.
// #define OnUnreadableTexture_ThrowError

// If a Texture2D has isReadable set to false then use the asset loader API to
// change the asset's import settings to make the Texture2D readable from now on.
// This solution offers the best preformance because after the asset import settings
// are changed the problem is resolved, however it also makes perminant changes
// to your Texture2D's import settings which can cause undesired behaviour. Always
// take backups before selecting this option just in case.
// #define OnUnreadableTexture_MakeTextureReadable





// The following throws an error if no OnUnreadableTexture2D action was selected
// or if more than one OnUnreadableTexture2D action was selected.
#if (OnUnreadableTexture_ReadFileDirectly && OnUnreadableTexture_ThrowError) || (OnUnreadableTexture_ThrowError && OnUnreadableTexture_MakeTextureReadable) || (OnUnreadableTexture_MakeTextureReadable && OnUnreadableTexture_ReadFileDirectly)
#error Only one OnUnreadableTexture action may be selected at a time.
#endif
#if !OnUnreadableTexture_ReadFileDirectly && !OnUnreadableTexture_ThrowError && !OnUnreadableTexture_MakeTextureReadable
#error Please select one OnUnreadableTexture action so that PixelCollider2Ds known what to do if a Texture2D is unreadable.
#endif





using System.Collections.Generic;
using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelCollider2D : MonoBehaviour
{
    // Pixes are considered solid if their {Channel} is {Selector} {Threshold}.
    public enum Channel : int
    {
        Alpha, Brightness, Red, Green, Blue
    }
    public Channel channel = Channel.Alpha;
    public enum Selector : int
    {
        GreaterThan,
        LessThan
    }
    public Selector selector = Selector.GreaterThan;
    [Range(0, 1)]
    public float threshold = 0.5f;

    private SpriteRenderer spriteRenderer = null;
    private PolygonCollider2D polygonCollider = null;

    public void Regenerate()
    {
        threshold = Math.Clamp(threshold, 0.0f, 1.0f);
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer is null)
        {
        }
        polygonCollider = GetComponent<PolygonCollider2D>();
        if (polygonCollider is null)
        {
        }
        Vector2[][] polygons = Trace(spriteRenderer.sprite);
        polygonCollider.pathCount = polygons.Length;
        for (int i = 0; i < polygons.Length; i++)
        {
            polygonCollider.SetPath(i, polygons[i]);
        }
    }
    public Vector2[][] Trace(Sprite sprite)
    {
        Vector2Int[][] pixelPolygons = Trace(sprite.texture);
        Vector2[][] polygons = new Vector2[pixelPolygons.Length][];
        for (int i = 0; i < pixelPolygons.Length; i++)
        {
            polygons[i] = new Vector2[pixelPolygons[i].Length];
            for (int j = 0; j < pixelPolygons[i].Length; j++)
            {
                // TODO implament an actual scaling function here.
                polygons[i][j] = new Vector2(pixelPolygons[i][j].x, pixelPolygons[i][j].y);
            }
        }
        return polygons;
    }
    public Vector2Int[][] Trace(Texture2D texture)
    {
        if (texture is null)
        {
            return new Vector2Int[0][];
        }
        if (!texture.isReadable)
        {
            texture = HandleUnreadableTexture(texture);
        }
        return TraceReadable(texture);
    }
    public Texture2D HandleUnreadableTexture(Texture2D texture)
    {
#if OnUnreadableTexture_ThrowError || !UNITY_EDITOR
        throw new Exception($"Texture2D \"{texture.name}\" could not be read. For help see the Readability section in ReadMe.md ");
#elif OnUnreadableTexture_ReadFileDirectly
        Debug.LogWarning($"Texture2D \"{texture.name}\" could not be read. Reading file directly.");
        string textureAssetPath = AssetDatabase.GetAssetPath(texture);
        byte[] rawTextureBytes = System.IO.File.ReadAllBytes(textureAssetPath);
        // Note that the initial size of loadedTexture does not matter because LoadImage will overwrite it.
        Texture2D loadedTexture = new Texture2D(1, 1);
        texture.LoadImage(rawTextureBytes);
        return loadedTexture;
#elif OnUnreadableTexture_MakeTextureReadable
        Debug.LogWarning($"Texture2D \"{texture.name}\" could not be read. Changing texture import settings.");
        string textureAssetPath = AssetDatabase.GetAssetPath(texture);
        TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(textureAssetPath);
        textureImporter.isReadable = true;
        AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
        Texture2D loadedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
        return loadedTexture;
#endif
    }
    private Vector2Int[][] TraceReadable(Texture2D readableTexture)
    {
        // TODO implament an actual tracing algorithem here.
        /*
        Should work by connecting unit line segments end to end left to right then up and down to create long vertical and horizontal segments.
        Finally go one by one comparing each vertical segment with each horizontal one and connecting them end to end if possible.
        Finally you are left with the finished polygons and can return.
        Note use linked lists during the computation process because they are super fast for add and remove operations.
        Convert to an array at the end only
        Finally handle edge cases where corners overlap. These are really the only weird situations but ill figure it out.
         */
        return new Vector2Int[0][];
    }
    public bool IsPixelSolid(Color pixel)
    {
        float value;
        switch (channel)
        {
            case Channel.Red:
                value = pixel.r;
                break;
            case Channel.Green:
                value = pixel.g;
                break;
            case Channel.Blue:
                value = pixel.b;
                break;
            case Channel.Brightness:
                value = (pixel.r + pixel.g + pixel.b) / 3.0f;
                break;
            default:
                value = pixel.a;
                break;
        }

        if (selector is Selector.LessThan)
        {
            return value < threshold;
        }
        else
        {
            return value > threshold;
        }
    }
}





/*
This custom inspector adds the "Regenerate Collider" button in the Unity editor
for PixelCollider2D components as well as other pretty menu's in the Unity inspector.
*/
#if UNITY_EDITOR
[CustomEditor(typeof(PixelCollider2D))]
public class PixelCollider2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PixelCollider2D pixelCollider2D = (PixelCollider2D)target;
        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
        GUILayout.Label("Pixes are considered solid if their ", GUILayout.ExpandWidth(false));
        if (EditorGUIUtility.currentViewWidth <= 525.0f)
        {
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
        }
        pixelCollider2D.channel = GUILayoutDropdown(pixelCollider2D.channel, "PixelCollider2DEditor.ChannelDropdown");
        GUILayout.Label(" is ", GUILayout.ExpandWidth(false));
        pixelCollider2D.selector = GUILayoutDropdown(pixelCollider2D.selector, "PixelCollider2DEditor.SelectorDropdown");
        GUILayout.Label(" ", GUILayout.ExpandWidth(false));
        pixelCollider2D.threshold = Mathf.Clamp(EditorGUILayout.DelayedFloatField(pixelCollider2D.threshold, GUILayout.ExpandWidth(false), GUILayout.MaxWidth(90.0f)), 0.0f, 1.0f);
        if (EditorGUIUtility.currentViewWidth <= 675.0f)
        {
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            pixelCollider2D.threshold = GUILayout.HorizontalSlider(pixelCollider2D.threshold, 0.0f, 1.0f);
            // Without this empty lable Unity gives my slider a height of 0 and it's invisible.
            GUILayout.Label("", GUILayout.ExpandWidth(false), GUILayout.MaxWidth(0.0f));
        }
        else
        {
            GUILayout.Label(" ", GUILayout.ExpandWidth(false));
            pixelCollider2D.threshold = GUILayout.HorizontalSlider(pixelCollider2D.threshold, 0.0f, 1.0f);
            GUILayout.Label(" ", GUILayout.ExpandWidth(false));
        }
        GUILayout.EndHorizontal();
        // This empty lable act's as a line break.
        GUILayout.Label("", GUILayout.ExpandWidth(false), GUILayout.MaxWidth(0.0f));
        if (GUILayout.Button("Regenerate Collider"))
        {
            pixelCollider2D.Regenerate();
        }
    }
    // Takes a string like "MyVariableNameHere" and turns it into "my variable name here ".
    private string FormatDropdownOption(string value)
    {
        System.Text.StringBuilder output = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]))
            {
                if (output.Length > 0)
                {
                    output.Append(' ');
                }
                output.Append(char.ToLower(value[i]));
            }
            else
            {
                output.Append(value[i]);
            }
        }
        output.Append(' ');
        return output.ToString();
    }
    // Dropdown ID is used to update the correct dropdown when a user selects an option.
    // Because selecting a dropdown option invokes a callback we can't easily return
    // the newly selected value from GUILayoutDropdown. Instead we add it to the list of
    // dropdowns with updated statuses and force an editor refresh by calling Repaint();
    private List<Tuple<string, object>> updatedDropdowns = new List<Tuple<string, object>>();
    private T GUILayoutDropdown<T>(T value, string dropdownID) where T : struct, Enum
    {
        if (EditorGUILayout.DropdownButton(new GUIContent(FormatDropdownOption(value.ToString())), FocusType.Keyboard, GUILayout.ExpandWidth(false)))
        {
            GenericMenu menu = new GenericMenu();
            foreach (T enumValue in Enum.GetValues(typeof(T)))
            {
                menu.AddItem(new GUIContent(FormatDropdownOption(enumValue.ToString())), value.Equals(enumValue), () =>
                {
                    lock (updatedDropdowns)
                    {
                        updatedDropdowns.Add(new Tuple<string, object>(dropdownID, enumValue));
                    }
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }
        lock (updatedDropdowns)
        {
            for (int i = 0; i < updatedDropdowns.Count; i++)
            {
                if (updatedDropdowns[i].Item1 == dropdownID)
                {
                    value = (T)updatedDropdowns[i].Item2;
                    updatedDropdowns.RemoveAt(i);
                    break;
                }
            }
        }
        return value;
    }
}
#endif