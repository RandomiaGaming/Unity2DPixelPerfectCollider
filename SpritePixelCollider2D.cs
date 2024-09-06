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

// Using statements. Note that trying to use the UnityEditor namespace in a release build causes errors.
// So the #if UNITY_EDITOR statement is added to prevent this.
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelCollider2D : MonoBehaviour
{
    public PixelSolidCondition pixelSolidCondition;

    private SpriteRenderer spriteRenderer = null;
    private PolygonCollider2D polygonCollider = null;

    public void Regenerate()
    {
        pixelSolidCondition.Threshold = Mathf.Clamp01(pixelSolidCondition.Threshold);
        bool forceReloadComponents = false;
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            forceReloadComponents = true;
        }
#endif
        if (spriteRenderer is null || forceReloadComponents)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        if (polygonCollider is null || forceReloadComponents)
        {
            polygonCollider = GetComponent<PolygonCollider2D>();
        }
        Vector2[][] polygons = PixelTracingHelper.TraceSprite(spriteRenderer.sprite, pixelSolidCondition);
        polygonCollider.pathCount = polygons.Length;
        for (int i = 0; i < polygons.Length; i++)
        {
            polygonCollider.SetPath(i, polygons[i]);
        }
    }
}

// Stores a conditional statement for determining if a pixel is solid.
// PixelSolidConditions always come in the following form:
// Pixes are considered solid if their {Channel} is {Condition} {Threshold}.
public struct PixelSolidCondition
{
    public enum ChannelType : int
    {
        Alpha = 0,
        Brightness = 1,
        Red = 2,
        Green = 3,
        Blue = 4
    }
    public ChannelType Channel;
    public enum ConditionType : int
    {
        GreaterThan = 0,
        LessThan = 1
    }
    public ConditionType Condition;
    public float Threshold;

    public PixelSolidCondition(ChannelType channelType, ConditionType conditionType, float threshold)
    {
        Channel = channelType;
        Condition = conditionType;
        Threshold = Mathf.Clamp01(threshold);
    }

    public bool IsPixelSolid(Color pixel)
    {
        float value;
        switch (Channel)
        {
            case ChannelType.Red:
                value = pixel.r;
                break;
            case ChannelType.Green:
                value = pixel.g;
                break;
            case ChannelType.Blue:
                value = pixel.b;
                break;
            case ChannelType.Brightness:
                value = (pixel.r + pixel.g + pixel.b) / 3.0f;
                break;
            default:
                value = pixel.a;
                break;
        }

        if (Condition is ConditionType.LessThan)
        {
            return value < Threshold;
        }
        else
        {
            return value > Threshold;
        }
    }
}

// A static helper class containing the actually pixel perfect outline tracing algorithem.
// Other components and classes make use of the PixelTracingHelper for their internal logic.
public static class PixelTracingHelper
{
    public static Vector2[][] TraceSprite(Sprite sprite, PixelSolidCondition pixelSolidCondition)
    {
        if (sprite is null)
        {
            return new Vector2[0][];
        }

        Vector2Int[][] pixelPolygons = TraceTexture(sprite.texture, pixelSolidCondition, new RectInt((int)sprite.rect.xMin, (int)sprite.rect.yMin, (int)sprite.rect.width, (int)sprite.rect.height));

        float scale = 1.0f / sprite.pixelsPerUnit;
        float offsetX = -(sprite.pivot.x * scale);
        float offsetY = -(sprite.pivot.y * scale);
        Vector2[][] polygons = new Vector2[pixelPolygons.Length][];
        for (int i = 0; i < pixelPolygons.Length; i++)
        {
            polygons[i] = new Vector2[pixelPolygons[i].Length];
            for (int j = 0; j < pixelPolygons[i].Length; j++)
            {
                polygons[i][j] = new Vector2((pixelPolygons[i][j].x * scale) + offsetX, (pixelPolygons[i][j].y * scale) + offsetY);
            }
        }
        return polygons;
    }
    public static Vector2Int[][] TraceTexture(Texture2D texture, PixelSolidCondition pixelSolidCondition, RectInt? rect = null)
    {
        if (texture is null || texture.width == 0 || texture.height == 0)
        {
            return new Vector2Int[0][];
        }
        if (rect == null)
        {
            rect = new RectInt(0, 0, texture.width, texture.height);
        }
        if (rect.Value.width == 0 || rect.Value.height == 0)
        {
            return new Vector2Int[0][];
        }
        if (!texture.isReadable)
        {
            texture = HandleUnreadableTexture(texture);
        }
        return TraceTextureInternal(texture, pixelSolidCondition, (RectInt)rect);
    }
    private static Texture2D HandleUnreadableTexture(Texture2D texture)
    {
#if OnUnreadableTexture_ThrowError
        throw new Exception($"Texture2D \"{texture.name}\" could not be read. For help see the Readability section in ReadMe.md ");
#elif OnUnreadableTexture_ReadFileDirectly
#if UNITY_EDITOR
        string textureAssetPath = AssetDatabase.GetAssetPath(texture);
        if (!System.IO.File.Exists(textureAssetPath))
        {
            throw new System.Exception("Texture2D does not have a source image file and therefore could not be read directly.");
        }
        if (EditorApplication.isPlaying)
        {
            /*
            When reading the pixel data from a Texture2D to generate a pixel perfect collider shape based upon
            that Texture2D we run into troubles if Texture2D.isReadable == false. In this case one option is to
            read the pixel data from the image file directly (.png, .jpg, ext). However this method only works in
            the Unity editor when asset image files are availible on the developers hard drive. Once the game is
            built in release mode and shipped to customers the image files will no longer be availible and this
            method will fail. As such it is worth exploring other solutions to this problem if you need to Regernate
            PixelCollider2Ds at runtime. See the top of this file for OnTextureUnreadable actions.
            */
            Debug.LogWarning("Texture2D was read directly from a file during runtime. This only works in debug builds not release builds.");
        }
        byte[] rawTextureBytes = System.IO.File.ReadAllBytes(textureAssetPath);
        // Note that the initial size of loadedTexture does not matter because LoadImage will overwrite it.
        Texture2D loadedTexture = new Texture2D(1, 1);
        loadedTexture.LoadImage(rawTextureBytes);
        return loadedTexture;
#else
        throw new Exception($"Texture2D cannot be read from file directly in a release build.");
#endif
#elif OnUnreadableTexture_MakeTextureReadable
#if UNITY_EDITOR
        string textureAssetPath = AssetDatabase.GetAssetPath(texture);
        if (!System.IO.File.Exists(textureAssetPath))
        {
            throw new Exception("Texture2D does not have a source image file and therefore could not be reimported as a readable Texture2D.");
        }
        TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(textureAssetPath);
        textureImporter.isReadable = true;
        AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
        Texture2D loadedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
        return loadedTexture;
#else
        throw new Exception($"Texture2D cannot be reimported as a readable Texture2D in a release build.");
#endif
#endif
    }
    private struct LineSegment
    {
        public int startX;
        public int startY;
        public int endX;
        public int endY;
        public LineSegment(int startx, int starty, int endx, int endy)
        {
            startX = startx;
            startY = starty;
            endX = endx;
            endY = endy;
        }
    }
    private static Vector2Int[][] TraceTextureInternal(Texture2D readableTexture, PixelSolidCondition pixelSolidCondition, RectInt rect)
    {
        // KNOWN ISSUE
        // Pixels which touch only on the corners cause the tracing algorithem to malfunction and generate
        // unstable colliders which only work sometimes. A patch for this issue is planned for later this week.

        // Read all the pixels from the source image all at once.
        // This is way faster than reading the pixels one at a time.
        // Additionally if we are reading the whole texture we can save even more time by not specifying a rect.
        Color[] pixelData;
        if (rect.x == 0 && rect.y == 0 && rect.width == readableTexture.width && rect.height == readableTexture.height)
        {
            pixelData = readableTexture.GetPixels();
        }
        else
        {
            pixelData = readableTexture.GetPixels(rect.x, rect.y, rect.width, rect.height);
        }

        // Cache the width and height of our rect for faster access
        int width = rect.width;
        int widthMinusOne = width - 1;
        int height = rect.height;
        int heightMinusOne = height - 1;

        // Compute weather each pixel is solid only once to save time.
        // Then we can just look up values from the solidityMap later on.
        bool[] solidityMap = new bool[pixelData.Length];
        for (int i = 0; i < pixelData.Length; i++)
        {
            solidityMap[i] = pixelSolidCondition.IsPixelSolid(pixelData[i]);
        }

        // Add line segements for each boarder between a solid and non-solid pixel on the x-axis.
        LineSegment currentLineSegment = new LineSegment(-1, -1, -1, -1);
        List<LineSegment> horizontalLineSegments = new List<LineSegment>();
        for (int y = 0; y <= height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool createLineSegment = false;
                if (y == 0 && solidityMap[(0 * width) + x])
                {
                    createLineSegment = true;
                }
                else if (y == height && solidityMap[(heightMinusOne * width) + x])
                {
                    createLineSegment = true;
                }
                else if (y != 0 && y != height && solidityMap[((y - 1) * width) + x] != solidityMap[(y * width) + x])
                {
                    createLineSegment = true;
                }

                if (createLineSegment)
                {
                    if (currentLineSegment.startX == -1)
                    {
                        currentLineSegment = new LineSegment(x, y, x + 1, y);
                    }
                    else
                    {
                        currentLineSegment.endX = x + 1;
                    }
                }
                else if (currentLineSegment.startX != -1)
                {
                    horizontalLineSegments.Add(currentLineSegment);
                    currentLineSegment = new LineSegment(-1, -1, -1, -1);
                }
            }
            if (currentLineSegment.startX != -1)
            {
                horizontalLineSegments.Add(currentLineSegment);
                currentLineSegment = new LineSegment(-1, -1, -1, -1);
            }
        }

        // Add line segements for each boarder between a solid and non-solid pixel on the x-axis.
        currentLineSegment = new LineSegment(-1, -1, -1, -1);
        List<LineSegment> verticalLineSegments = new List<LineSegment>();
        for (int x = 0; x <= width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool createLineSegment = false;
                if (x == 0 && solidityMap[(y * width) + 0])
                {
                    createLineSegment = true;
                }
                else if (x == width && solidityMap[(y * width) + widthMinusOne])
                {
                    createLineSegment = true;
                }
                else if (x != 0 && x != width && solidityMap[(y * width) + (x - 1)] != solidityMap[(y * width) + x])
                {
                    createLineSegment = true;
                }

                if (createLineSegment)
                {
                    if (currentLineSegment.startY == -1)
                    {
                        currentLineSegment = new LineSegment(x, y, x, y + 1);
                    }
                    else
                    {
                        currentLineSegment.endY = y + 1;
                    }
                }
                else if (currentLineSegment.startY != -1)
                {
                    verticalLineSegments.Add(currentLineSegment);
                    currentLineSegment = new LineSegment(-1, -1, -1, -1);
                }
            }
            if (currentLineSegment.startY != -1)
            {
                verticalLineSegments.Add(currentLineSegment);
                currentLineSegment = new LineSegment(-1, -1, -1, -1);
            }
        }

        // Combine all the vertical and horizontal line segments into polygons.
        List<Vector2Int[]> output = new List<Vector2Int[]>();
        while (horizontalLineSegments.Count > 0)
        {
            bool god = false;
            if (god)
            {
                throw null;
            }

            List<Vector2Int> currentPolygon = new List<Vector2Int>() { new Vector2Int(horizontalLineSegments[0].startX, horizontalLineSegments[0].startY), new Vector2Int(horizontalLineSegments[0].endX, horizontalLineSegments[0].endY) };
            horizontalLineSegments.RemoveAt(0);
            while (true)
            {
                for (int i = 0; i < verticalLineSegments.Count; i++)
                {
                    Vector2Int lastPoitnInCurrentPolygon = currentPolygon[currentPolygon.Count - 1];
                    LineSegment verticalLineSegment = verticalLineSegments[i];
                    if (verticalLineSegment.startX == lastPoitnInCurrentPolygon.x && verticalLineSegment.startY == lastPoitnInCurrentPolygon.y)
                    {
                        currentPolygon.Add(new Vector2Int(verticalLineSegment.endX, verticalLineSegment.endY));
                        verticalLineSegments.RemoveAt(i);
                        break;
                    }
                    else if (verticalLineSegment.endX == lastPoitnInCurrentPolygon.x && verticalLineSegment.endY == lastPoitnInCurrentPolygon.y)
                    {
                        currentPolygon.Add(new Vector2Int(verticalLineSegment.startX, verticalLineSegment.startY));
                        verticalLineSegments.RemoveAt(i);
                        break;
                    }
                }
                if (currentPolygon[0] == currentPolygon[currentPolygon.Count - 1])
                {
                    break;
                }
                for (int i = 0; i < horizontalLineSegments.Count; i++)
                {
                    Vector2Int lastPoitnInCurrentPolygon = currentPolygon[currentPolygon.Count - 1];
                    LineSegment horizontalLineSegment = horizontalLineSegments[i];
                    if (horizontalLineSegment.startX == lastPoitnInCurrentPolygon.x && horizontalLineSegment.startY == lastPoitnInCurrentPolygon.y)
                    {
                        currentPolygon.Add(new Vector2Int(horizontalLineSegment.endX, horizontalLineSegment.endY));
                        horizontalLineSegments.RemoveAt(i);
                        break;
                    }
                    else if (horizontalLineSegment.endX == lastPoitnInCurrentPolygon.x && horizontalLineSegment.endY == lastPoitnInCurrentPolygon.y)
                    {
                        currentPolygon.Add(new Vector2Int(horizontalLineSegment.startX, horizontalLineSegment.startY));
                        horizontalLineSegments.RemoveAt(i);
                        break;
                    }
                }
            }
            output.Add(currentPolygon.ToArray());
        }
        return output.ToArray();
    }
}

// This custom inspector adds the "Regenerate Collider" button in the Unity editor
// for PixelCollider2D components as well as other pretty menu's in the Unity inspector.
#if UNITY_EDITOR
[CustomEditor(typeof(PixelCollider2D))]
public class PixelCollider2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PixelCollider2D pixelCollider2D = (PixelCollider2D)target;
        pixelCollider2D.pixelSolidCondition = GUILayoutHelper.PixelSolidConditionSelector(pixelCollider2D.pixelSolidCondition, this, "pixelSolidCondition");
        // This empty lable acts as a line break.
        GUILayout.Label("", GUILayout.ExpandWidth(false), GUILayout.MaxWidth(0.0f));
        if (GUILayout.Button("Regenerate Collider"))
        {
            pixelCollider2D.Regenerate();
        }
    }
}
#endif

#if UNITY_EDITOR
public static class GUILayoutHelper
{
    // Draws a custom inspector menu allowing users to construct their own PixelSolidConditions
    // Validates user input and returns the new PixelSolidCondition based on user selections.
    public static PixelSolidCondition PixelSolidConditionSelector(PixelSolidCondition value, Editor customInspector, string selectorID)
    {
        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
        GUILayout.Label("Pixes are considered solid if their", GUILayout.ExpandWidth(false));
        if (EditorGUIUtility.currentViewWidth <= 525.0f)
        {
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
        }
        value.Channel = EnumDropdown(value.Channel, customInspector, selectorID + ".Channel");
        GUILayout.Label("is", GUILayout.ExpandWidth(false));
        value.Condition = EnumDropdown(value.Condition, customInspector, selectorID + ".Condition");
        value.Threshold = Mathf.Clamp01(EditorGUILayout.DelayedFloatField(value.Threshold, GUILayout.ExpandWidth(false), GUILayout.MaxWidth(90.0f)));
        if (EditorGUIUtility.currentViewWidth <= 675.0f)
        {
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            value.Threshold = GUILayout.HorizontalSlider(value.Threshold, 0.0f, 1.0f);
            // Without this empty lable Unity gives my slider a height of 0 and it's invisible.
            GUILayout.Label("", GUILayout.ExpandWidth(false), GUILayout.MaxWidth(0.0f));
        }
        else
        {
            GUILayout.Label(" ", GUILayout.ExpandWidth(false));
            value.Threshold = GUILayout.HorizontalSlider(value.Threshold, 0.0f, 1.0f);
            GUILayout.Label(" ", GUILayout.ExpandWidth(false));
        }
        GUILayout.EndHorizontal();
        return value;
    }

    // Used to store when a dropdown is updated.
    // When a user selects a dropdown option an asynchronous callback is invoked signalling
    // the change. However in order to return this new value to the user from calls to Dropdown
    // we must save the new value and which dropdown it was associated with so we can return
    // the new value at the next call to Dropdown.
    private struct DropdownUpdatePacket
    {
        public object NewValue;
        public Editor CustomInspector;
        public string DropdownID;
        public DropdownUpdatePacket(object newValue, Editor customInspector, string dropdownID)
        {
            NewValue = newValue;
            CustomInspector = customInspector;
            DropdownID = dropdownID;
        }
    }
    private static List<DropdownUpdatePacket> _updatedDropdowns = new List<DropdownUpdatePacket>();
    // Creates a dropdown menu to select an enum value.
    public static T EnumDropdown<T>(T value, Editor customInspector, string dropdownID) where T : struct, System.Enum
    {
        T[] values = (T[])System.Enum.GetValues(typeof(T));
        string[] names = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            names[i] = FormatName(values[i].ToString());
        }
        return Dropdown(value, FormatName(value.ToString()), values, names, customInspector, dropdownID);
    }
    // Creates a dropdown menu to select from a collection of values where each value has a name.
    public static T Dropdown<T>(T value, string valueName, T[] values, string[] names, Editor customInspector, string dropdownID)
    {
        if (EditorGUILayout.DropdownButton(new GUIContent(valueName), FocusType.Keyboard, GUILayout.ExpandWidth(false)))
        {
            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < values.Length; i++)
            {
                T currentValue = values[i];
                menu.AddItem(new GUIContent(names[i]), value.Equals(values[i]), () =>
                {
                    lock (_updatedDropdowns)
                    {
                        _updatedDropdowns.Add(new DropdownUpdatePacket(currentValue, customInspector, dropdownID));
                    }
                    customInspector.Repaint();
                });
            }
            menu.ShowAsContext();
        }
        lock (_updatedDropdowns)
        {
            for (int i = 0; i < _updatedDropdowns.Count; i++)
            {
                if (_updatedDropdowns[i].CustomInspector == customInspector && _updatedDropdowns[i].DropdownID == dropdownID)
                {
                    value = (T)_updatedDropdowns[i].NewValue;
                    _updatedDropdowns.RemoveAt(i);
                    break;
                }
            }
        }
        return value;
    }

    // With LowercaseEnumNames == true all capital letters will be made lower case.
    // For example "MyEnumValue" would become "myenumvalue" when displayed to the user.
    // With LowercaseEnumNames == false the original string is displayed unchanged.
    public static bool LowercaseEnumNames = true;
    // With SpaceSplitEnumNames == true all capital letters will be preceeded by a space.
    // For example "MyEnumValue" would become "My Enum Value" when displayed to the user.
    // With SpaceSplitEnumNames == false the original string is displayed unchanged.
    public static bool SpaceSplitEnumNames = true;
    // Formats a string based on the settings above.
    private static string FormatName(string name)
    {
        System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                if (stringBuilder.Length > 0 && SpaceSplitEnumNames)
                {
                    stringBuilder.Append(' ');
                }
                if (LowercaseEnumNames)
                {
                    stringBuilder.Append(char.ToLower(name[i]));
                }
                else
                {
                    stringBuilder.Append(name[i]);
                }
            }
            else
            {
                stringBuilder.Append(name[i]);
            }
        }
        return stringBuilder.ToString();
    }
}
#endif