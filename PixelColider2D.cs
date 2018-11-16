using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public class PixelColider2D : MonoBehaviour
{
    private SpriteRenderer sr;
    private PolygonCollider2D pc;
    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        pc = GetComponent<PolygonCollider2D>();
        Regenerate();
    }
    public void Regenerate()
    {
        List<Vector2> newpoints = new List<Vector2>();
        for (int height = 0; height < sr.sprite.texture.height; height++)
        {
            for (int width = 0; width < sr.sprite.texture.width; width++)
            {
                if (sr.sprite.texture.GetPixel(width, height).a > 0)
                {
                    if (sr.sprite.texture.GetPixel(width + 1, height + 1).a == 0 && sr.sprite.texture.GetPixel(width + 1, height).a == sr.sprite.texture.GetPixel(width, height + 1).a)
                    {
                        newpoints.Add(TextureToCollider(new Vector2(width + 1, height + 1), sr.sprite));
                    }
                    if (sr.sprite.texture.GetPixel(width - 1, height - 1).a == 0 && sr.sprite.texture.GetPixel(width - 1, height).a == sr.sprite.texture.GetPixel(width, height - 1).a)
                    {
                        newpoints.Add(TextureToCollider(new Vector2(width, height), sr.sprite));
                    }
                    if (sr.sprite.texture.GetPixel(width - 1, height + 1).a == 0 && sr.sprite.texture.GetPixel(width - 1, height).a == sr.sprite.texture.GetPixel(width, height + 1).a)
                    {
                        newpoints.Add(TextureToCollider(new Vector2(width, height + 1), sr.sprite));
                    }
                    if (sr.sprite.texture.GetPixel(width + 1, height - 1).a == 0 && sr.sprite.texture.GetPixel(width + 1, height).a == sr.sprite.texture.GetPixel(width, height - 1).a)
                    {
                        newpoints.Add(TextureToCollider(new Vector2(width + 1, height), sr.sprite));
                    }
                }
            }
        }
        List<Vector2> input = newpoints;
        newpoints = new List<Vector2>();
        Vector2 currentpoint = input[0];
        newpoints.Add(currentpoint);
        int i = 0;
        while (newpoints.Count != input.Count)
        {
            i++;
            if (i > 100)
            {
                break;
            }
            Vector2 best = new Vector2(float.MaxValue, float.MaxValue);
            foreach (Vector2 point in input)
            {
                if (!Contains(newpoints.ToArray(), point) && point.y == currentpoint.y)
                {
                    if(Mathf.Abs(currentpoint.x - point.x) < Mathf.Abs(currentpoint.x - best.x))
                    {
                        best = point;
                    }
                }
            }
            if(best != new Vector2(float.MaxValue, float.MaxValue))
            {
                newpoints.Add(best);
                currentpoint = best;
            }
            best = new Vector2(float.MaxValue, float.MaxValue);
            foreach (Vector2 point in input)
            {
                if (!Contains(newpoints.ToArray(), point) && point.x == currentpoint.x)
                {
                    if (Mathf.Abs(currentpoint.y - point.y) < Mathf.Abs(currentpoint.y - best.y))
                    {
                        best = point;
                    }
                }
            }
            if (best != new Vector2(float.MaxValue, float.MaxValue))
            {
                newpoints.Add(best);
                currentpoint = best;
            }
           
        }

        pc.points = newpoints.ToArray();
    }
    public bool Contains(Vector2[] input, Vector2 contains)
    {
        foreach (Vector2 v in input)
        {
            if (v.x == contains.x && v.y == contains.y)
            {
                return true;
            }
        }
        return false;
    }
    public Vector2 TextureToCollider(Vector2 pos, Sprite texture)
    {

        float distance = Mathf.Abs(texture.bounds.max.x - texture.bounds.min.x);
        pos.x *= distance;
        pos.x /= texture.texture.width;
        float distance2 = Mathf.Abs(texture.bounds.max.y - texture.bounds.min.y);
        pos.y *= distance2;
        pos.y /= texture.texture.height;

        return pos;
    }
}
