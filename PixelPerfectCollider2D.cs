using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

[RequireComponent(typeof(PolygonCollider2D))]
public class PixelPerfectCollider2D : MonoBehaviour
{
    [Space]
    [Tooltip("All pixels with an alpha value greater than or equal to the AlphaThreshhold are considered solid.")]
    [Range(0, 1)]
    public float AlphaThreshhold = 0.5f;
    public void Regenerate()
    {
        //Test that all references are not null.
        if (GetComponent<PolygonCollider2D>() == null)
        {
            gameObject.AddComponent<PolygonCollider2D>();
            Debug.LogWarning("No polygonCollider2D component was found on " + gameObject.name + " so a new one was added.");
        }
        PolygonCollider2D polygoncollider = GetComponent<PolygonCollider2D>();
        if (GetComponent<SpriteRenderer>() == null || GetComponent<SpriteRenderer>().sprite == null)
        {
            polygoncollider.pathCount = 0;
            return;
        }
        //If the execution got this far then all the references are good so we can assign them.
        SpriteRenderer spriterenderer = GetComponent<SpriteRenderer>();
        Sprite sprite = spriterenderer.sprite;
        //Here we make a rendertexture copy of our texture to make it readable.
        Texture2D texture = ForceReadable(sprite.texture);
        List<ColliderSegment> segments;
        //Get all the one pixel long segments that will makeup our collider.
        segments = GetSegments(texture);
        List<List<Vector2>> paths;
        //Finally we trace paths that connect all the segments.
        paths = FindPaths(segments);
        //Convert to localspace.
        paths = ConvertToLocal(paths, sprite);
        //Move relative to the pivot.
        paths = CalculatePivot(paths, sprite);
        //And last we tell the polygon collider to start using our new data.
        polygoncollider.pathCount = paths.Count;
        for (int p = 0; p < paths.Count; p++)
        {
            polygoncollider.SetPath(p, paths[p].ToArray());
        }
    }

    //This method moves the collider so that it is relative to the sprite's pivot.
    private List<List<Vector2>> CalculatePivot(List<List<Vector2>> original, Sprite sprite)
    {
        Vector2 pivot = sprite.pivot;
        float distance = Mathf.Abs(sprite.bounds.max.x - sprite.bounds.min.x);
        pivot.x *= distance;
        pivot.x /= sprite.texture.width;
        float distance2 = Mathf.Abs(sprite.bounds.max.y - sprite.bounds.min.y);
        pivot.y *= distance2;
        pivot.y /= sprite.texture.height;

        for (int p = 0; p < original.Count; p++)
        {
            for (int o = 0; o < original[p].Count; o++)
            {
                original[p][o] -= pivot;
            }
        }
        return original;
    }

    //this struct will store data about sections of collider.
    private struct ColliderSegment
    {
        public Vector2 Point1;
        public Vector2 Point2;
        public ColliderSegment(Vector2 Point1, Vector2 Point2)
        {
            this.Point1 = Point1;
            this.Point2 = Point2;
        }
    }

    private List<List<Vector2>> ConvertToLocal(List<List<Vector2>> original, Sprite sprite)
    {
        for (int p = 0; p < original.Count; p++)
        {
            for (int o = 0; o < original[p].Count; o++)
            {
                Vector2 currentpoint = original[p][o];
                float distance = Mathf.Abs(sprite.bounds.max.x - sprite.bounds.min.x);
                currentpoint.x *= distance;
                currentpoint.x /= sprite.texture.width;
                float distance2 = Mathf.Abs(sprite.bounds.max.y - sprite.bounds.min.y);
                currentpoint.y *= distance2;
                currentpoint.y /= sprite.texture.height;
                original[p][o] = currentpoint;
            }
        }
        return original;
    }

    //This function traces along the segments and connects them together into paths.
    List<List<Vector2>> FindPaths(List<ColliderSegment> segments)
    {
        List<List<Vector2>> output = new List<List<Vector2>>();
        while (segments.Count > 0)
        {
            Vector2 currentpoint = segments[0].Point2;
            List<Vector2> currentpath = new List<Vector2> { segments[0].Point1, segments[0].Point2 };
            segments.Remove(segments[0]);

            bool currentpathcomplete = false;
            while (currentpathcomplete == false)
            {
                currentpathcomplete = true;
                for (int s = 0; s < segments.Count; s++)
                {
                    if (segments[s].Point1 == currentpoint)
                    {
                        currentpathcomplete = false;
                        currentpath.Add(segments[s].Point2);
                        currentpoint = segments[s].Point2;
                        segments.Remove(segments[s]);
                    }
                    else if (segments[s].Point2 == currentpoint)
                    {
                        currentpathcomplete = false;
                        currentpath.Add(segments[s].Point1);
                        currentpoint = segments[s].Point1;
                        segments.Remove(segments[s]);
                    }
                }
            }
            output.Add(currentpath);
        }
        return output;
    }

    //This function finds the one pixel long segments that make up the collider.
    List<ColliderSegment> GetSegments(Texture2D texture)
    {
        List<ColliderSegment> output = new List<ColliderSegment>();
        //Loop over each pixel.
        for (int height = 0; height < texture.height; height++)
        {
            for (int width = 0; width < texture.width; width++)
            {
                //First check that the current pixel is solid.
                if (texture.GetPixel(width, height).a >= AlphaThreshhold)
                {
                    //if it is check the pixels above, bellow, to the left, and to the right to see if they are edges.
                    if (height + 1 >= texture.height || texture.GetPixel(width, height + 1).a < AlphaThreshhold)
                    {
                        output.Add(new ColliderSegment(new Vector2(width, height + 1), new Vector2(width + 1, height + 1)));
                    }
                    if (height - 1 < 0 || texture.GetPixel(width, height - 1).a < AlphaThreshhold)
                    {
                        output.Add(new ColliderSegment(new Vector2(width, height), new Vector2(width + 1, height)));
                    }
                    if (width + 1 >= texture.width || texture.GetPixel(width + 1, height).a < AlphaThreshhold)
                    {
                        output.Add(new ColliderSegment(new Vector2(width + 1, height), new Vector2(width + 1, height + 1)));
                    }
                    if (width - 1 < 0 || texture.GetPixel(width - 1, height).a < AlphaThreshhold)
                    {
                        output.Add(new ColliderSegment(new Vector2(width, height), new Vector2(width, height + 1)));
                    }
                }
            }
        }
        return output;
    }

    //This function forces unity to allow us to read a texture.
    private Texture2D ForceReadable(Texture2D original)
    {
        //I barely understand this so thank you to Sergio Gomez for showing me this.
        //Find his work here "https://support.unity3d.com/hc/en-us/articles/206486626-How-can-I-get-pixels-from-unreadable-textures".
        Texture2D Copy = new Texture2D(original.width, original.height);
        RenderTexture tmp = RenderTexture.GetTemporary(original.width, original.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(original, tmp);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = tmp;
        Copy.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
        Copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(tmp);
        return Copy;
    }
}

//This class gives unity custom instructions for how to show our component and adds the regenerate button to the inspector.
[CustomEditor(typeof(PixelPerfectCollider2D))]
public class PixelColider2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Regenerate Collider!"))
        {
            PixelPerfectCollider2D collider = (PixelPerfectCollider2D)target;
            collider.Regenerate();
        }
        base.OnInspectorGUI();
    }
}