using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

public class SpatialLookup : MonoBehaviour
{

    public struct mapping
    {
        public uint Key;
        public int Value;

        public mapping(uint Key, int Value)
        {
            this.Key = Key;
            this.Value = Value;
        }
    }

    public static mapping[] spatialLookup = new mapping[simulator.particleNumber];
    public static int[] startIndices = new int[simulator.particleNumber];

    public static void updateSpatialLookup(Vector2[] points, float radius)
    {
        spatialLookup = new mapping[simulator.particleNumber];
        startIndices = new int[simulator.particleNumber];

        Parallel.For(0, points.Length, i =>
        {
            (int x, int y) = PositionToCellCoord(points[i], radius);
            uint cellKey = HashCell(x, y);
            spatialLookup[i] = new mapping(cellKey, i);
            startIndices[i] = int.MaxValue;
        });

        quickSort(0, spatialLookup.Length - 1);

        Parallel.For(0, points.Length, i =>
        {
            uint key = spatialLookup[i].Key;
            uint keyPrev = i == 0 ? uint.MaxValue : spatialLookup[i - 1].Key;
            if (key != keyPrev)
            {
                startIndices[key] = i;
            }
        });

    }

    public static List<int> pointsWithinRadius(Vector2[] points, Vector2 samplePoint, float radius)
    {
        (int x, int y) = PositionToCellCoord(samplePoint, radius);

        List<int> output = new List<int>();

        for (int i = -1; i < 2; i++)
        {
            for (int j = -1; j < 2; j++)
            {
                uint key = HashCell(x + i, y + j);
                int startIndex = startIndices[key];

                for (int k = startIndex; k < startIndices.Length; k++)
                {
                    if (spatialLookup[k].Key != key) break;

                    int particleIndex = spatialLookup[k].Value;
                    float distance = (points[particleIndex] - samplePoint).magnitude;

                    if (distance < simulator.particleNumber)
                    {
                        output.Add(particleIndex);
                    }
                }

            }
        }

        return output;
    }
    public static (int x, int y) PositionToCellCoord(Vector2 pos, float radius)
    {
        return ((int)(pos.x / radius), (int)(pos.y / radius));
    }

    public static uint HashCell(int x, int y)
    {
        return ((uint)(x * 15823) + (uint)(y * 9737333)) % (uint)startIndices.Length;
    }

    public static void swap(int i, int j)
    {
        mapping temp = spatialLookup[i];
        spatialLookup[i] = spatialLookup[j];
        spatialLookup[j] = temp;
    }

    public static int partition(int low, int high)
    {
        uint pivot = spatialLookup[high].Key;

        int k = low;

        for(int i = low; i < high; i++)
        {
            if(spatialLookup[i].Key < pivot)
            {
                swap(k, i);
                k++;
            }
        }

        swap(k, high);

        return k;
    }

    public static void quickSort(int low, int high)
    {
        if(low < high)
        {
            int k = partition(low, high);

            quickSort(low, k - 1);
            quickSort(k + 1, high);
        }
    }


}
