// Select how you would like to handle unreadable textures by uncommenting one of the following #define statements:

// If a texture is unreadable then use the asset loader to locate the file within the assets folder and read the pixel data directly from the file with System.IO. This option is recommended because it always works, however it can be quite slow because loading the image from disk each time takes a while.
// #define OnUnreadableTexture_ReadFileDirectly

// If a texture is unreadable then throw a System.Exception. This option can generate a lot of errors since textures are not readable by default, however it gives the most user control.
// #define OnUnreadableTexture_ThrowError

// If a texture is unreadable then use the asset loader to change the texture's settings to enable read/write access. This option offers the best performance because it makes the texture readable from now on thereby fixing any future errors, however it makes permanent changes to your texture's settings which may cause other issues.
#define OnUnreadableTexture_MakeTextureReadable



// The following preprocessor directives throw an error if no OnUnreadableTexture action is selected or if more than one is selected.
#if (OnUnreadableTexture_ReadFileDirectly && OnUnreadableTexture_ThrowError) || (OnUnreadableTexture_ThrowError && OnUnreadableTexture_MakeTextureReadable) || (OnUnreadableTexture_MakeTextureReadable && OnUnreadableTexture_ReadFileDirectly)
#error Only one OnUnreadableTexture action may be selected at a time.
#endif
#if !OnUnreadableTexture_ReadFileDirectly && !OnUnreadableTexture_ThrowError && !OnUnreadableTexture_MakeTextureReadable
#error Please select an OnUnreadableTexture action.
#endif

// Note that trying to use the UnityEditor namespace in a release build causes errors. So the #if UNITY_EDITOR preprocessor directive is required to prevent this. Additionally preprocessor directives are used to exclude all editor code from release builds.
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.U2D.Sprites;
#endif

// This component allows you to generate pixel perfect polygon colliders in one click!
[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelCollider2D : MonoBehaviour
{
    public PixelSolidCondition pixelSolidCondition = PixelSolidCondition.Default;

    private SpriteRenderer spriteRenderer = null;
    private PolygonCollider2D polygonCollider = null;

    // Generates a pixel perfect outline of a sprite renderer and applies it to a polygon collider.
    public void Regenerate()
    {
        pixelSolidCondition.Threshold = Mathf.Clamp01(pixelSolidCondition.Threshold);

        // When in edit mode components change frequently so it's never safe to assume an old reference is still valid.
        bool forceReloadComponents = false;
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            forceReloadComponents = true;
        }
#endif
        if (spriteRenderer == null || forceReloadComponents)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        if (polygonCollider == null || forceReloadComponents)
        {
            polygonCollider = GetComponent<PolygonCollider2D>();
        }

        // Trace the sprite and apply the new polygons to the PolygonCollider2D.
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
// Pixels are considered solid if their {Channel} is {Condition} {Threshold}.
// For example:
// Pixels are considered solid if their alpha is greater than 0.5.
public struct PixelSolidCondition
{
    public static readonly PixelSolidCondition Default = new PixelSolidCondition(ChannelType.Alpha, ConditionType.GreaterThan, 0.5f);

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

    public readonly bool IsPixelSolid(Color pixel)
    {
        float value;
        switch (Channel)
        {
            case ChannelType.Brightness:
                value = (pixel.r + pixel.g + pixel.b) / 3.0f;
                break;
            case ChannelType.Red:
                value = pixel.r;
                break;
            case ChannelType.Green:
                value = pixel.g;
                break;
            case ChannelType.Blue:
                value = pixel.b;
                break;
            default:
                value = pixel.a;
                break;
        };

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

// A static helper class containing the actual pixel perfect outline tracing algorithm. All other components and classes make use of the PixelTracingHelper for their internal logic.
public static class PixelTracingHelper
{
    // Traces a pixel perfect outline of a given texture and reimports that texture with the new outline as its physics shape. If the given texture is a sprite sheet then physics shapes are generated and applied for each sprite individually.
    // Note this causes permanent changes to the texture's physics shape.
    public static void TraceAndApplyPhysicsShape(Texture2D texture, PixelSolidCondition pixelSolidCondition)
    {
        string textureAssetPath = AssetDatabase.GetAssetPath(texture);
        TextureImporter textureImporter = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
        SpriteDataProviderFactories spriteDataProviderFactory = new SpriteDataProviderFactories();
        spriteDataProviderFactory.Init();
        ISpriteEditorDataProvider spriteEditorDataProvider = spriteDataProviderFactory.GetSpriteEditorDataProviderFromObject(textureImporter);
        spriteEditorDataProvider.InitSpriteEditorDataProvider();
        ISpritePhysicsOutlineDataProvider physicsOutlineDataProvider = spriteEditorDataProvider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();

        SpriteRect[] spriteRects = spriteEditorDataProvider.GetSpriteRects();
        for (int i = 0; i < spriteRects.Length; i++)
        {
            SpriteRect spriteRect = spriteRects[i];
            RectInt pixelRect = new RectInt((int)spriteRect.rect.x, (int)spriteRect.rect.y, (int)spriteRect.rect.width, (int)spriteRect.rect.height);
            Vector2Int[][] pixelPolygons = TraceTexture(texture, pixelSolidCondition, pixelRect);
            Vector2[][] polygons = new Vector2[pixelPolygons.Length][];
            float offsetX = -(pixelRect.width / 2.0f);
            float offsetY = -(pixelRect.height / 2.0f);
            for (int j = 0; j < pixelPolygons.Length; j++)
            {
                Vector2Int[] pixelPolygon = pixelPolygons[j];
                Vector2[] polygon = new Vector2[pixelPolygon.Length];
                for (int k = 0; k < pixelPolygon.Length; k++)
                {
                    polygon[k] = new Vector2(pixelPolygon[k].x + offsetX, pixelPolygon[k].y + offsetY);
                }
                polygons[j] = polygon;
            }
            physicsOutlineDataProvider.SetOutlines(spriteRect.spriteID, new List<Vector2[]>(polygons));
        }

        spriteEditorDataProvider.Apply();
        EditorUtility.SetDirty(textureImporter);
        textureImporter.SaveAndReimport();
        AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
    }

    // Traces a pixel perfect outline of a given sprite using TraceTexture for the initial shape. Then scales that shape based upon the sprite's pixels per unit and pivot.
    public static Vector2[][] TraceSprite(Sprite sprite, PixelSolidCondition pixelSolidCondition)
    {
        if (sprite == null)
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
            Vector2Int[] pixelPolygon = pixelPolygons[i];
            Vector2[] polygon = new Vector2[pixelPolygon.Length];
            for (int j = 0; j < pixelPolygon.Length; j++)
            {
                polygon[j] = new Vector2((pixelPolygon[j].x * scale) + offsetX, (pixelPolygon[j].y * scale) + offsetY);
            }
            polygons[i] = polygon;
        }
        return polygons;
    }

    // Traces a pixel perfect outline of a given texture. Optionally traces only a small subsection within the texture as defined by rect.
    public static Vector2Int[][] TraceTexture(Texture2D texture, PixelSolidCondition pixelSolidCondition, RectInt? rect = null)
    {
        if (texture == null || texture.width == 0 || texture.height == 0)
        {
            return new Vector2Int[0][];
        }
        rect ??= new RectInt(0, 0, texture.width, texture.height);
        if (rect.Value.width == 0 || rect.Value.height == 0)
        {
            throw new System.Exception("Rect cannot have a width or height of 0.");
        }
        if (rect.Value.xMin > 0 || rect.Value.yMin > 0 || rect.Value.xMax > texture.width || rect.Value.yMax > texture.height)
        {
            throw new System.Exception("Rect must be contained within the bounds of texture.");
        }
        if (!texture.isReadable)
        {
            texture = HandleUnreadableTexture(texture);
        }
        return TraceTextureInternal(texture, pixelSolidCondition, (RectInt)rect);
    }

    // Handles an unreadable texture by performing the action specified by the current OnUnreadableTexture setting. For more information read the comments at the very top of this file.
    private static Texture2D HandleUnreadableTexture(Texture2D texture)
    {
#if OnUnreadableTexture_ThrowError
        throw new System.Exception($"Texture was unreadable.");
#elif OnUnreadableTexture_ReadFileDirectly
#if UNITY_EDITOR
        string textureAssetPath = AssetDatabase.GetAssetPath(texture);
        if (!System.IO.File.Exists(textureAssetPath))
        {
            throw new System.Exception("Texture does not have an associated asset file and therefore could not be read directly.");
        }
        if (EditorApplication.isPlaying)
        {
            // Additional information on the following warning:
            // When reading the pixel data from a texture to generate a pixel perfect outline we run into troubles if Texture2D.isReadable == false. In that case one option is to read the pixel data from the image file directly (.png or .jpg). However this method only works in the unity editor where asset source files are accessible. Once the game has been built in release mode the asset source files will no longer be available and this method will fail. As such it is recommended to use other options if you need to trace textures at runtime. Scroll to the very top of this file for other OnTextureUnreadable actions.
            Debug.LogWarning("Texture was read directly from the asset file during runtime. This only works in debug builds and is considered unstable.");
        }
        byte[] rawTextureBytes = System.IO.File.ReadAllBytes(textureAssetPath);
        // Note that the initial size of loadedTexture does not matter because LoadImage will overwrite it.
        Texture2D loadedTexture = new Texture2D(1, 1);
        loadedTexture.LoadImage(rawTextureBytes);
        return loadedTexture;
#else
        throw new System.Exception($"Textures cannot be read directly from the asset file in a release build.");
#endif
#elif OnUnreadableTexture_MakeTextureReadable
#if UNITY_EDITOR
        string textureAssetPath = AssetDatabase.GetAssetPath(texture);
        if (!System.IO.File.Exists(textureAssetPath))
        {
            throw new System.Exception("Texture does not have an associated asset file and therefore could not be made readable.");
        }
        TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(textureAssetPath);
        textureImporter.isReadable = true;
        EditorUtility.SetDirty(textureImporter);
        textureImporter.SaveAndReimport();
        AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
        Texture2D loadedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
        return loadedTexture;
#else
        throw new System.Exception($"Textures cannot be made readable in a release build.");
#endif
#endif
    }

    // Efficiently stores a simple 2D line segment in pixel space.
    private struct LineSegment
    {
        public int StartX;
        public int StartY;
        public int EndX;
        public int EndY;
        public LineSegment(int startX, int startY, int endX, int endY)
        {
            StartX = startX;
            StartY = startY;
            EndX = endX;
            EndY = endY;
        }
    }

    // Traces a pixel perfect outline of a given texture. Unlike the public version of TraceTexture this method requires that the texture be readable and that rect not be null. This method contains the actual algorithm implementation.
    private static Vector2Int[][] TraceTextureInternal(Texture2D texture, PixelSolidCondition pixelSolidCondition, RectInt rect)
    {
        // KNOWN ISSUE
        // Pixels which touch only on the corners cause the tracing algorithm to malfunction and generate unstable colliders which only work sometimes. A patch for this issue is planned for later this week.

        // Read all the pixels from the source image all at once. This is way faster than reading the pixels one at a time. Additionally if we are reading the whole texture we can save even more time by not specifying a rect.
        Color[] pixelData;
        if (rect.x == 0 && rect.y == 0 && rect.width == texture.width && rect.height == texture.height)
        {
            pixelData = texture.GetPixels();
        }
        else
        {
            pixelData = texture.GetPixels(rect.x, rect.y, rect.width, rect.height);
        }

        // Compute whether each pixel is solid only once to save time. Then we can just look up values from the solidityMap later on.
        bool[] solidityMap = new bool[pixelData.Length];
        for (int i = 0; i < pixelData.Length; i++)
        {
            solidityMap[i] = pixelSolidCondition.IsPixelSolid(pixelData[i]);
        }

        // Cache the width and height of our rect for faster access
        int width = rect.width;
        int widthMinusOne = width - 1;
        int height = rect.height;
        int heightMinusOne = height - 1;

        // Add line segments for each border between a solid and non-solid pixel on the x-axis.
        LineSegment currentLineSegment = new LineSegment(-1, -1, -1, -1);
        LinkedList<LineSegment> horizontalLineSegments = new LinkedList<LineSegment>();
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
                    if (currentLineSegment.StartX == -1)
                    {
                        currentLineSegment = new LineSegment(x, y, x + 1, y);
                    }
                    else
                    {
                        currentLineSegment.EndX = x + 1;
                    }
                }
                else if (currentLineSegment.StartX != -1)
                {
                    horizontalLineSegments.AddLast(currentLineSegment);
                    currentLineSegment = new LineSegment(-1, -1, -1, -1);
                }
            }
            if (currentLineSegment.StartX != -1)
            {
                horizontalLineSegments.AddLast(currentLineSegment);
                currentLineSegment = new LineSegment(-1, -1, -1, -1);
            }
        }

        // Add line segments for each border between a solid and non-solid pixel on the y-axis.
        currentLineSegment = new LineSegment(-1, -1, -1, -1);
        LinkedList<LineSegment> verticalLineSegments = new LinkedList<LineSegment>();
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
                    if (currentLineSegment.StartY == -1)
                    {
                        currentLineSegment = new LineSegment(x, y, x, y + 1);
                    }
                    else
                    {
                        currentLineSegment.EndY = y + 1;
                    }
                }
                else if (currentLineSegment.StartY != -1)
                {
                    verticalLineSegments.AddLast(currentLineSegment);
                    currentLineSegment = new LineSegment(-1, -1, -1, -1);
                }
            }
            if (currentLineSegment.StartY != -1)
            {
                verticalLineSegments.AddLast(currentLineSegment);
                currentLineSegment = new LineSegment(-1, -1, -1, -1);
            }
        }

        // Combine all the vertical and horizontal line segments into polygons.
        LinkedList<Vector2Int[]> polygons = new LinkedList<Vector2Int[]>();
        while (horizontalLineSegments.Count > 0)
        {
            LinkedList<Vector2Int> currentPolygon = new LinkedList<Vector2Int>();
            currentPolygon.AddLast(new Vector2Int(horizontalLineSegments.First.Value.StartX, horizontalLineSegments.First.Value.StartY));
            currentPolygon.AddLast(new Vector2Int(horizontalLineSegments.First.Value.EndX, horizontalLineSegments.First.Value.EndY));
            horizontalLineSegments.RemoveFirst();

            while (true)
            {
                {
                    LinkedListNode<LineSegment> currentNode = verticalLineSegments.First;
                    for (int i = 0; i < verticalLineSegments.Count; i++)
                    {
                        Vector2Int lastPointInCurrentPolygon = currentPolygon.Last.Value;
                        LineSegment verticalLineSegment = currentNode.Value;
                        if (verticalLineSegment.StartX == lastPointInCurrentPolygon.x && verticalLineSegment.StartY == lastPointInCurrentPolygon.y)
                        {
                            currentPolygon.AddLast(new Vector2Int(verticalLineSegment.EndX, verticalLineSegment.EndY));
                            verticalLineSegments.Remove(currentNode);
                            break;
                        }
                        else if (verticalLineSegment.EndX == lastPointInCurrentPolygon.x && verticalLineSegment.EndY == lastPointInCurrentPolygon.y)
                        {
                            currentPolygon.AddLast(new Vector2Int(verticalLineSegment.StartX, verticalLineSegment.StartY));
                            verticalLineSegments.Remove(currentNode);
                            break;
                        }
                        currentNode = currentNode.Next;
                    }
                }

                if (currentPolygon.First.Value.x == currentPolygon.Last.Value.x && currentPolygon.First.Value.y == currentPolygon.Last.Value.y)
                {
                    break;
                }

                {
                    LinkedListNode<LineSegment> currentNode = horizontalLineSegments.First;
                    for (int i = 0; i < horizontalLineSegments.Count; i++)
                    {
                        Vector2Int lastPointInCurrentPolygon = currentPolygon.Last.Value;
                        LineSegment horizontalLineSegment = currentNode.Value;
                        if (horizontalLineSegment.StartX == lastPointInCurrentPolygon.x && horizontalLineSegment.StartY == lastPointInCurrentPolygon.y)
                        {
                            currentPolygon.AddLast(new Vector2Int(horizontalLineSegment.EndX, horizontalLineSegment.EndY));
                            horizontalLineSegments.Remove(currentNode);
                            break;
                        }
                        else if (horizontalLineSegment.EndX == lastPointInCurrentPolygon.x && horizontalLineSegment.EndY == lastPointInCurrentPolygon.y)
                        {
                            currentPolygon.AddLast(new Vector2Int(horizontalLineSegment.StartX, horizontalLineSegment.StartY));
                            horizontalLineSegments.Remove(currentNode);
                            break;
                        }
                        currentNode = currentNode.Next;
                    }
                }
            }

            Vector2Int[] currentPolygonArray = new Vector2Int[currentPolygon.Count];
            {
                LinkedListNode<Vector2Int> currentNode = currentPolygon.First;
                for (int i = 0; i < currentPolygon.Count; i++)
                {
                    currentPolygonArray[i] = currentNode.Value;
                    currentNode = currentNode.Next;
                }
            }
            polygons.AddLast(currentPolygonArray);
        }

        // Convert polygons linked list into normal array.
        Vector2Int[][] polygonsArray = new Vector2Int[polygons.Count][];
        {
            LinkedListNode<Vector2Int[]> currentNode = polygons.First;
            for (int i = 0; i < polygons.Count; i++)
            {
                polygonsArray[i] = currentNode.Value;
                currentNode = currentNode.Next;
            }
        }

        return polygonsArray;
    }
}

// This custom inspector adds the custom menus for PixelCollider2D components.
#if UNITY_EDITOR
[CustomEditor(typeof(PixelCollider2D))]
public class PixelCollider2DEditor : Editor
{
    // Rendering code for the PixelCollider2D custom inspector
    public override void OnInspectorGUI()
    {
        PixelCollider2D pixelCollider2D = (PixelCollider2D)target;
        pixelCollider2D.pixelSolidCondition = GUILayoutHelper.PixelSolidConditionSelector(pixelCollider2D.pixelSolidCondition, this, "pixelSolidCondition");
        GUILayout.Label("", GUILayout.ExpandWidth(false), GUILayout.MaxWidth(0.0f));
        if (GUILayout.Button("Regenerate Collider"))
        {
            pixelCollider2D.Regenerate();
        }
    }
}
#endif

// This custom editor window allows users to apply pixel perfect physics shapes to texture assets.
#if UNITY_EDITOR
public class PixelPhysicsShapeEditor : EditorWindow
{
    private struct SelectedTextureInfo
    {
        public Texture2D Texture;
        public string AssetPath;
        public GUID AssetGUID;

        public SelectedTextureInfo(Texture2D texture, string assetPath, GUID assetGUID)
        {
            AssetPath = assetPath;
            Texture = texture;
            AssetGUID = assetGUID;
        }
    }
    private readonly List<SelectedTextureInfo> selectedTextures = new List<SelectedTextureInfo>();
    private PixelSolidCondition pixelSolidCondition = PixelSolidCondition.Default;
    private Vector2 scrollPosition = Vector2.zero;

    // This adds the Window>Pixel Physics Shape Editor button.
    [MenuItem("Window/Pixel Physics Shape Editor")]
    public static void ShowWindow()
    {
        PixelPhysicsShapeEditor window = GetWindow<PixelPhysicsShapeEditor>("Pixel Physics Shape Editor");
        window.Show();
    }

    // Main GUI event handler for the pixel physics shape editor window.
    private void OnGUI()
    {
        try
        {
            switch (Event.current.type)
            {
                case EventType.MouseDown:
                case EventType.MouseUp:
                case EventType.MouseMove:
                case EventType.MouseDrag:
                case EventType.KeyDown:
                case EventType.KeyUp:
                case EventType.ScrollWheel:
                case EventType.Repaint:
                case EventType.Layout:
                case EventType.ContextClick:
                case EventType.MouseEnterWindow:
                case EventType.MouseLeaveWindow:
                case EventType.TouchDown:
                case EventType.TouchUp:
                case EventType.TouchMove:
                case EventType.TouchEnter:
                case EventType.TouchLeave:
                case EventType.TouchStationary:
                    UpdateGUI();
                    break;
                case EventType.DragUpdated:
                case EventType.DragPerform:
                case EventType.DragExited:
                    UpdateDragAndDrop();
                    break;
                case EventType.ValidateCommand:
                case EventType.ExecuteCommand:
                case EventType.Ignore:
                case EventType.Used:
                    // Ignore these events
                    break;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            Debug.LogWarning("Pixel physics shape editor window has crashed.");
            Close();
        }
    }

    // Normal rendering event handler for the pixel physics shape editor window.
    private void UpdateGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Width(position.width), GUILayout.Height(position.height));

        pixelSolidCondition = GUILayoutHelper.PixelSolidConditionSelector(pixelSolidCondition, this, "pixelSolidCondition");
        GUILayout.Label("", GUILayout.ExpandWidth(false), GUILayout.MaxWidth(0.0f));

        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
        GUILayout.Label($"Selected Textures ({selectedTextures.Count}):", GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Clear All", GUILayout.ExpandWidth(false)))
        {
            selectedTextures.Clear();
        }
        GUILayout.EndHorizontal();

        for (int i = 0; i < selectedTextures.Count; i++)
        {
            SelectedTextureInfo selectedTexture = selectedTextures[i];
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            GUILayout.Label($"{selectedTexture.Texture.name} ({selectedTexture.AssetPath})", GUILayout.ExpandWidth(true));
            if (GUILayout.Button(" Remove ", GUILayout.ExpandWidth(false)))
            {
                selectedTextures.RemoveAt(i);
                i--;
            }
            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Generate And Apply Physics Shapes"))
        {
            if (selectedTextures.Count == 0)
            {
                EditorUtility.DisplayDialog("No textures selected", "In order to generate and apply pixel perfect physics shapes you first need to select some textures. To select textures drag and drop them onto this window.", "Okie Dokie");
            }
            else
            {
                bool confirm = EditorUtility.DisplayDialog(
                    $"Are you sure?",
                    $"Are you sure you want to change the physics shape of {selectedTextures.Count} {(selectedTextures.Count == 1 ? "texture?" : "textures?")}",
                    "Yes",
                    "No (cancel and go back)"
                );

                if (confirm)
                {
                    for (int i = 0; i < selectedTextures.Count; i++)
                    {
                        SelectedTextureInfo selectedTexture = selectedTextures[i];
                        try
                        {
                            PixelTracingHelper.TraceAndApplyPhysicsShape(selectedTexture.Texture, pixelSolidCondition);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogException(new System.Exception($"Failed to regenerate physics shape for texture {selectedTexture.Texture.name} at {selectedTexture.AssetPath} due to exception: {ex.Message}"));
                        }
                    }
                    selectedTextures.Clear();
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // Drag and drop event handler for the pixel physics shape editor window.
    private void UpdateDragAndDrop()
    {
        switch (Event.current.type)
        {
            case EventType.DragUpdated:
                DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                break;
            case EventType.DragExited:
                DragAndDrop.visualMode = DragAndDropVisualMode.None;
                break;
            case EventType.DragPerform:
                DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                DragAndDrop.AcceptDrag();
                Object[] droppedObjects = DragAndDrop.objectReferences;
                for (int i = 0; i < droppedObjects.Length; i++)
                {
                    OnDrop(droppedObjects[i]);
                }
                break;
        }
    }

    // Private methods to handle drag and drop for various types of objects.
    private void OnDrop(Object droppedObject)
    {
        if (droppedObject is Texture2D)
        {
            Texture2D droppedTexture = (Texture2D)droppedObject;
            OnDropTexture(droppedTexture);
        }
        else if (droppedObject is DefaultAsset)
        {
            DefaultAsset droppedAsset = (DefaultAsset)droppedObject;
            OnDropFolder(droppedAsset);
        }
    }
    private void OnDropTexture(Texture2D droppedTexture)
    {
        if (droppedTexture == null)
        {
            return;
        }
        string droppedAssetPath = AssetDatabase.GetAssetPath(droppedTexture);
        GUID droppedAssetGUID = AssetDatabase.GUIDFromAssetPath(droppedAssetPath);
        for (int i = 0; i < selectedTextures.Count; i++)
        {
            if (selectedTextures[i].AssetGUID == droppedAssetGUID)
            {
                // Don't add duplicate textures.
                return;
            }
        }
        selectedTextures.Add(new SelectedTextureInfo(droppedTexture, droppedAssetPath, droppedAssetGUID));
    }
    private void OnDropFolder(DefaultAsset droppedFolder)
    {
        string droppedFolderPath = AssetDatabase.GetAssetPath(droppedFolder);
        if (System.IO.Directory.Exists(droppedFolderPath))
        {
            string[] droppedFiles = System.IO.Directory.GetFiles(droppedFolderPath, "*", System.IO.SearchOption.AllDirectories);
            for (int i = 0; i < droppedFiles.Length; i++)
            {
                OnDropFile(droppedFiles[i]);
            }
        }
    }
    private void OnDropFile(string droppedFile)
    {
        string ext = System.IO.Path.GetExtension(droppedFile).ToLower();
        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
        {
            Texture2D droppedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(droppedFile);
            if (!(droppedTexture == null))
            {
                OnDropTexture(droppedTexture);
            }
        }
    }
}
#endif

// This helper class contains many useful methods for creating menus in the unity editor.
#if UNITY_EDITOR
public static class GUILayoutHelper
{
    // Draws a custom menu allowing users to construct their own PixelSolidConditions. Validates user input and returns the new PixelSolidCondition based on user selections.
    public static PixelSolidCondition PixelSolidConditionSelector(PixelSolidCondition value, Editor context, string selectorID)
    {
        // 18.0f is the size of the padding in DIPs between the edge of the inspector window and the actual content area.
        return PixelSolidConditionSelectorInternal(value, context, EditorGUIUtility.currentViewWidth - 18.0f, selectorID);
    }
    public static PixelSolidCondition PixelSolidConditionSelector(PixelSolidCondition value, EditorWindow context, string selectorID)
    {
        return PixelSolidConditionSelectorInternal(value, context, EditorGUIUtility.currentViewWidth, selectorID);
    }
    private static PixelSolidCondition PixelSolidConditionSelectorInternal(PixelSolidCondition value, object context, float contextWidth, string selectorID)
    {
        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
        GUILayout.Label("Pixels are considered solid if their", GUILayout.ExpandWidth(false));
        // 481.0f is the width in DIPs of a pixel solid condition selector. If the current context is too small then part of the selector should be placed on the next line.
        if (contextWidth <= 481.0f)
        {
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
        }
        value.Channel = EnumDropdownInternal(value.Channel, context, selectorID + ".Channel");
        GUILayout.Label("is", GUILayout.ExpandWidth(false));
        value.Condition = EnumDropdownInternal(value.Condition, context, selectorID + ".Condition");
        value.Threshold = Mathf.Clamp01(EditorGUILayout.DelayedFloatField(value.Threshold, GUILayout.ExpandWidth(false), GUILayout.MaxWidth(90.0f)));
        GUILayout.EndHorizontal();
        return value;
    }

    // Used to store when a dropdown is updated. When a user selects a dropdown option an asynchronous callback is invoked signaling the change. In order to return this new value to the user from calls to GUILayoutHelper.Dropdown we must save the new value and which dropdown it was associated with so we can return the correct value.
    private struct DropdownUpdatePacket
    {
        public object NewValue;
        public object Context;
        public string DropdownID;
        public DropdownUpdatePacket(object newValue, object context, string dropdownID)
        {
            NewValue = newValue;
            Context = context;
            DropdownID = dropdownID;
        }
    }
    private static readonly List<DropdownUpdatePacket> updatedDropdowns = new List<DropdownUpdatePacket>();

    // Creates a dropdown menu to select a value from a given enum.
    public static T EnumDropdown<T>(T value, Editor context, string dropdownID) where T : struct, System.Enum
    {
        return EnumDropdownInternal(value, context, dropdownID);
    }
    public static T EnumDropdown<T>(T value, EditorWindow context, string dropdownID) where T : struct, System.Enum
    {
        return EnumDropdownInternal(value, context, dropdownID);
    }
    private static T EnumDropdownInternal<T>(T value, object context, string dropdownID) where T : struct, System.Enum
    {
        T[] values = (T[])System.Enum.GetValues(typeof(T));
        string[] names = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            names[i] = FormatName(values[i].ToString());
        }
        return DropdownInternal(value, FormatName(value.ToString()), values, names, context, dropdownID);
    }

    // Creates a dropdown menu to select from a collection of name and value pairs.
    public static T Dropdown<T>(T value, string valueName, T[] values, string[] names, Editor context, string dropdownID)
    {
        return DropdownInternal(value, valueName, values, names, context, dropdownID);
    }
    public static T Dropdown<T>(T value, string valueName, T[] values, string[] names, EditorWindow context, string dropdownID)
    {
        return DropdownInternal(value, valueName, values, names, context, dropdownID);
    }
    private static T DropdownInternal<T>(T value, string valueName, T[] values, string[] names, object context, string dropdownID)
    {
        if (EditorGUILayout.DropdownButton(new GUIContent(valueName), FocusType.Keyboard, GUILayout.ExpandWidth(false)))
        {
            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < values.Length; i++)
            {
                T currentValue = values[i];
                menu.AddItem(new GUIContent(names[i]), value.Equals(currentValue), () =>
                {
                    lock (updatedDropdowns)
                    {
                        updatedDropdowns.Add(new DropdownUpdatePacket(currentValue, context, dropdownID));
                    }
                    context.GetType().GetMethod("Repaint").Invoke(context, null);
                });
            }
            menu.ShowAsContext();
        }
        lock (updatedDropdowns)
        {
            for (int i = 0; i < updatedDropdowns.Count; i++)
            {
                DropdownUpdatePacket updatedDropdown = updatedDropdowns[i];
                if (updatedDropdown.Context == context && updatedDropdown.DropdownID == dropdownID)
                {
                    value = (T)updatedDropdown.NewValue;
                    updatedDropdowns.RemoveAt(i);
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
    // With SpaceSplitEnumNames == true all capital letters will be preceded by a space.
    // For example "MyEnumValue" would become "My Enum Value" when displayed to the user.
    // With SpaceSplitEnumNames == false the original string is displayed unchanged.
    public static bool SpaceSplitEnumNames = true;
    // Formats a string based on the settings above.
    private static string FormatName(string name)
    {
        System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c))
            {
                if (stringBuilder.Length > 0 && SpaceSplitEnumNames)
                {
                    stringBuilder.Append(' ');
                }
                if (LowercaseEnumNames)
                {
                    stringBuilder.Append(char.ToLower(c));
                }
                else
                {
                    stringBuilder.Append(c);
                }
            }
            else
            {
                stringBuilder.Append(c);
            }
        }
        return stringBuilder.ToString();
    }
}
#endif