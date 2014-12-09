// Copyright (c) 2009-2011 Ignacio Castano <castano@gmail.com>
// Copyright (c) 2007-2009 NVIDIA Corporation -- Ignacio Castano <icastano@nvidia.com>
// 
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NvidiaTextureTools
{
    class Fitting
    {
        private static void swap<T>(ref T a, ref T b)
        {
            T atmp = a;
            a = b;
            b = atmp;
        }

        static Vector3 computeCentroid(int n, Vector3[] points)
        {
            Vector3 centroid = Vector3.zero;

            for (int i = 0; i < n; i++)
            {
                centroid += points[i];
            }
            centroid /= (float)n;

            return centroid;
        }

        static Vector3 computeCentroid(int n, Vector3[] points, float[] weights, Vector3 metric)
        {
            Vector3 centroid = Vector3.zero;
            float total = 0.0f;

            for (int i = 0; i < n; i++)
            {
                total += weights[i];
                centroid += weights[i] * points[i];
            }
            centroid /= total;

            return centroid;
        }

        static Vector3 computeCovariance(int n, Vector3[] points, float[] weights, Vector3 metric, float[] covariance)
        {
            // compute the centroid
            Vector3 centroid = computeCentroid(n, points, weights, metric);

            // compute covariance matrix
            for (int i = 0; i < 6; i++)
            {
                covariance[i] = 0.0f;
            }

            for (int i = 0; i < n; i++)
            {
                Vector3 a = Vector3.Scale((points[i] - centroid), metric);
                Vector3 b = weights[i] * a;

                covariance[0] += a.x * b.x;
                covariance[1] += a.x * b.y;
                covariance[2] += a.x * b.z;
                covariance[3] += a.y * b.y;
                covariance[4] += a.y * b.z;
                covariance[5] += a.z * b.z;
            }

            return centroid;
        }

        static Vector3 estimatePrincipalComponent(float[] matrix)
        {
            Vector3 row0 = new Vector3(matrix[0], matrix[1], matrix[2]);
            Vector3 row1 = new Vector3(matrix[1], matrix[3], matrix[4]);
            Vector3 row2 = new Vector3(matrix[2], matrix[4], matrix[5]);

            float r0 = (row0).sqrMagnitude;
            float r1 = (row1).sqrMagnitude;
            float r2 = (row2).sqrMagnitude;

            if (r0 > r1 && r0 > r2) return row0;
            if (r1 > r2) return row1;
            return row2;
        }


        static Vector3 firstEigenVector_PowerMethod(float[] matrix)
        {
            if (matrix[0] == 0 && matrix[3] == 0 && matrix[5] == 0)
            {
                return Vector3.zero;
            }

            Vector3 v = estimatePrincipalComponent(matrix);

            const int NUM = 8;
            for (int i = 0; i < NUM; i++)
            {
                float x = v.x * matrix[0] + v.y * matrix[1] + v.z * matrix[2];
                float y = v.x * matrix[1] + v.y * matrix[3] + v.z * matrix[4];
                float z = v.x * matrix[2] + v.y * matrix[4] + v.z * matrix[5];

                float norm = Mathf.Max(Mathf.Max(x, y), z);

                v = new Vector3(x, y, z) / norm;
            }

            return v;
        }

        public static int compute4Means(int n, Vector3[] points, float[] weights, Vector3 metric, Vector3[] cluster)
        {
            // Compute principal component.
            float[] matrix = new float[6];
            Vector3 centroid = computeCovariance(n, points, weights, metric, matrix);
            Vector3 principal = firstEigenVector_PowerMethod(matrix);

            // Pick initial solution.
            int mini, maxi;
            mini = maxi = 0;

            float mindps, maxdps;
            mindps = maxdps = Vector3.Dot(points[0] - centroid, principal);

            for (int i = 1; i < n; ++i)
            {
                float dps = Vector3.Dot(points[i] - centroid, principal);

                if (dps < mindps)
                {
                    mindps = dps;
                    mini = i;
                }
                else
                {
                    maxdps = dps;
                    maxi = i;
                }
            }

            cluster[0] = centroid + mindps * principal;
            cluster[1] = centroid + maxdps * principal;
            cluster[2] = (2.0f * cluster[0] + cluster[1]) / 3.0f;
            cluster[3] = (2.0f * cluster[1] + cluster[0]) / 3.0f;

            // Now we have to iteratively refine the clusters.
            while (true)
            {
                Vector3[] newCluster = new Vector3[4] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };
                float[] total = new float[4] { 0, 0, 0, 0 };

                for (int i = 0; i < n; ++i)
                {
                    // Find nearest cluster.
                    int nearest = 0;
                    float mindist = float.MaxValue;
                    for (int j = 0; j < 4; j++)
                    {
                        float dist = Vector3.Scale((cluster[j] - points[i]), metric).sqrMagnitude;
                        if (dist < mindist)
                        {
                            mindist = dist;
                            nearest = j;
                        }
                    }

                    newCluster[nearest] += weights[i] * points[i];
                    total[nearest] += weights[i];
                }

                for (int j = 0; j < 4; j++)
                {
                    if (total[j] != 0)
                        newCluster[j] /= total[j];
                }

                if (equal(cluster[0], newCluster[0]) && equal(cluster[1], newCluster[1]) &&
                    equal(cluster[2], newCluster[2]) && equal(cluster[3], newCluster[3]))
                {
                    return ((total[0] != 0) ? 1 : 0) + ((total[1] != 0) ? 1 : 0) + ((total[2] != 0) ? 1 : 0) + ((total[3] != 0) ? 1 : 0);
                }

                cluster[0] = newCluster[0];
                cluster[1] = newCluster[1];
                cluster[2] = newCluster[2];
                cluster[3] = newCluster[3];

                // Sort clusters by weight.
                for (int i = 0; i < 4; i++)
                {
                    for (int j = i; j > 0 && total[j] > total[j - 1]; j--)
                    {
                        swap(ref total[j], ref total[j - 1]);
                        swap(ref cluster[j], ref cluster[j - 1]);
                    }
                }
            }
        }

        static bool equal(Vector3 vector1, Vector3 vector2)
        {
            return (Vector3.Distance(vector1, vector2) < 0.000001f);
        }
    }
}
