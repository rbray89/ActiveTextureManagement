using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace LibSquishPort
{

    public class Sym3x3
    {
        float[] m_x = new float[6];

        public Sym3x3()
        {
        }

        public Sym3x3(float s)
        {
            for (int i = 0; i < 6; ++i)
                m_x[i] = s;
        }

        public float this[int index]
        {
            get
            {
                return m_x[index];
            }

            set
            {
                m_x[index] = value;
            }
        }

    }

    public static class math
    {
        public static Sym3x3 ComputeWeightedCovariance(int n, Vector3[] points, float[] weights)
        {
            // compute the centroid
            float total = 0.0f;
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < n; ++i)
            {
                total += weights[i];
                centroid += weights[i] * points[i];
            }
            centroid /= total;

            // accumulate the covariance matrix
            Sym3x3 covariance = new Sym3x3();
            for (int i = 0; i < n; ++i)
            {
                Vector3 a = points[i] - centroid;
                Vector3 b = weights[i] * a;

                covariance[0] += a.x * b.x;
                covariance[1] += a.x * b.y;
                covariance[2] += a.x * b.z;
                covariance[3] += a.y * b.y;
                covariance[4] += a.y * b.z;
                covariance[5] += a.z * b.z;
            }

            // return it
            return covariance;
        }

        public static Vector3 GetMultiplicity1Evector(Sym3x3 matrix, float evalue)
        {
            // compute M
            Sym3x3 m = new Sym3x3();
            m[0] = matrix[0] - evalue;
            m[1] = matrix[1];
            m[2] = matrix[2];
            m[3] = matrix[3] - evalue;
            m[4] = matrix[4];
            m[5] = matrix[5] - evalue;

            // compute U
            Sym3x3 u = new Sym3x3();
            u[0] = m[3] * m[5] - m[4] * m[4];
            u[1] = m[2] * m[4] - m[1] * m[5];
            u[2] = m[1] * m[4] - m[2] * m[3];
            u[3] = m[0] * m[5] - m[2] * m[2];
            u[4] = m[1] * m[2] - m[4] * m[0];
            u[5] = m[0] * m[3] - m[1] * m[1];

            // find the largest component
            float mc = Mathf.Abs(u[0]);
            int mi = 0;
            for (int i = 1; i < 6; ++i)
            {
                float c = Mathf.Abs(u[i]);
                if (c > mc)
                {
                    mc = c;
                    mi = i;
                }
            }

            // pick the column with this component
            switch (mi)
            {
                case 0:
                    return new Vector3(u[0], u[1], u[2]);

                case 1:
                case 3:
                    return new Vector3(u[1], u[3], u[4]);

                default:
                    return new Vector3(u[2], u[4], u[5]);
            }
        }

        public static Vector3 GetMultiplicity2Evector(Sym3x3 matrix, float evalue)
        {
            // compute M
            Sym3x3 m = new Sym3x3();
            m[0] = matrix[0] - evalue;
            m[1] = matrix[1];
            m[2] = matrix[2];
            m[3] = matrix[3] - evalue;
            m[4] = matrix[4];
            m[5] = matrix[5] - evalue;

            // find the largest component
            float mc = Mathf.Abs(m[0]);
            int mi = 0;
            for (int i = 1; i < 6; ++i)
            {
                float c = Mathf.Abs(m[i]);
                if (c > mc)
                {
                    mc = c;
                    mi = i;
                }
            }

            // pick the first eigenvector based on this index
            switch (mi)
            {
                case 0:
                case 1:
                    return new Vector3(-m[1], m[0], 0.0f);

                case 2:
                    return new Vector3(m[2], 0.0f, -m[0]);

                case 3:
                case 4:
                    return new Vector3(0.0f, -m[4], m[3]);

                default:
                    return new Vector3(0.0f, -m[5], m[4]);
            }
        }

        public static Vector3 ComputePrincipleComponent(Sym3x3 matrix)
        {
            // compute the cubic coefficients
            float c0 = matrix[0] * matrix[3] * matrix[5]
                + 2.0f * matrix[1] * matrix[2] * matrix[4]
                - matrix[0] * matrix[4] * matrix[4]
                - matrix[3] * matrix[2] * matrix[2]
                - matrix[5] * matrix[1] * matrix[1];
            float c1 = matrix[0] * matrix[3] + matrix[0] * matrix[5] + matrix[3] * matrix[5]
                - matrix[1] * matrix[1] - matrix[2] * matrix[2] - matrix[4] * matrix[4];
            float c2 = matrix[0] + matrix[3] + matrix[5];

            // compute the quadratic coefficients
            float a = c1 - (1.0f / 3.0f) * c2 * c2;
            float b = (-2.0f / 27.0f) * c2 * c2 * c2 + (1.0f / 3.0f) * c1 * c2 - c0;

            // compute the root count check
            float Q = 0.25f * b * b + (1.0f / 27.0f) * a * a * a;

            // test the multiplicity
            if (float.Epsilon < Q)
            {
                // only one root, which implies we have a multiple of the identity
                return Vector3.one;
            }
            else if (Q < -float.Epsilon)
            {
                // three distinct roots
                float theta = Mathf.Atan2(Mathf.Sqrt(-Q), -0.5f * b);
                float rho = Mathf.Sqrt(0.25f * b * b - Q);

                float rt = Mathf.Pow(rho, 1.0f / 3.0f);
                float ct = Mathf.Cos(theta / 3.0f);
                float st = Mathf.Sin(theta / 3.0f);

                float l1 = (1.0f / 3.0f) * c2 + 2.0f * rt * ct;
                float l2 = (1.0f / 3.0f) * c2 - rt * (ct + (float)Mathf.Sqrt(3.0f) * st);
                float l3 = (1.0f / 3.0f) * c2 - rt * (ct - (float)Mathf.Sqrt(3.0f) * st);

                // pick the larger
                if (Mathf.Abs(l2) > Mathf.Abs(l1))
                    l1 = l2;
                if (Mathf.Abs(l3) > Mathf.Abs(l1))
                    l1 = l3;

                // get the eigenvector
                return GetMultiplicity1Evector(matrix, l1);
            }
            else // if( -FLT_EPSILON <= Q && Q <= FLT_EPSILON )
            {
                // two roots
                float rt;
                if (b < 0.0f)
                    rt = -Mathf.Pow(-0.5f * b, 1.0f / 3.0f);
                else
                    rt = Mathf.Pow(0.5f * b, 1.0f / 3.0f);

                float l1 = (1.0f / 3.0f) * c2 + rt;		// repeated
                float l2 = (1.0f / 3.0f) * c2 - 2.0f * rt;

                // get the eigenvector
                if (Mathf.Abs(l1) > Mathf.Abs(l2))
                    return GetMultiplicity2Evector(matrix, l1);
                else
                    return GetMultiplicity1Evector(matrix, l2);
            }
        }

        public static bool CompareAnyLessThan(Vector4 left, Vector4 right)
        {
            return left.x < right.x
                || left.y < right.y
                || left.z < right.z
                || left.w < right.w;
        }

        public static Vector4 MultiplyAdd(this Vector4 vector, Vector4 a, Vector4 b)
        {
            return Vector4.Scale(vector, a) + b;
        }

        public static Vector4 SplatX(this Vector4 vector)
        {
            return Vector4.one* vector.x;
        }

        public static Vector4 SplatY(this Vector4 vector)
        {
            return Vector4.one* vector.y;
        }

        public static Vector4 SplatZ(this Vector4 vector)
        {
            return Vector4.one* vector.z;
        }

        public static Vector4 SplatW(this Vector4 vector)
        {
            return Vector4.one* vector.w;
        }

        public static Vector4 NegativeMultiplySubtract(this Vector4 vector, Vector4 a, Vector4 b)
        {
            return b - Vector4.Scale(vector,a);
        }

        public static Vector4 Reciprocal(this Vector4 v)
        {
            return new Vector4(
            1.0f / v.x,
            1.0f / v.y,
            1.0f / v.z,
            1.0f / v.w);
        }

        public static Vector4 Truncate( this Vector4 v )
	    {
		    return new Vector4(
			    v.x > 0.0f ? Mathf.Floor( v.x ) : Mathf.Ceil( v.x ), 
			    v.y > 0.0f ? Mathf.Floor( v.y ) : Mathf.Ceil( v.y ), 
			    v.z > 0.0f ? Mathf.Floor( v.z ) : Mathf.Ceil( v.z ),
                v.w > 0.0f ? Mathf.Floor(v.w) : Mathf.Ceil(v.w)
		    );
	    }

    }


}
