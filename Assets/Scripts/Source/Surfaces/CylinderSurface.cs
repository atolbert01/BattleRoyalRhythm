using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BattleRoyalRhythm.Surfaces
{

    public sealed class CylinderSurface : Surface
    {
        [Tooltip("Radius of the cylindrical surface.")]
        [SerializeField][Min(1f)] private float radius = 5f;
        [Tooltip("The revolve angle of the surface.")]
        [SerializeField][Range(-360f, 360f)] private float angle = 90f;


        public override Mesh GetTileMesh()
        {
            int index = 0;
            int curveCuts = 3;
            // Generate vertices by layering the ring
            // base of the cylinder.
            Vector3[] bottomRing = GetRing(curveCuts);
            Vector3[] vertices = new Vector3[bottomRing.Length * (LengthY + 1)];
            for (int y = 0; y <= LengthY; y++)
            {
                for (int x = 0; x < bottomRing.Length; x++)
                {
                    vertices[index] = new Vector3(bottomRing[x].x, y, bottomRing[x].z);
                    index++;
                }
            }
            // Generate the triangles.
            index = 0;
            int[] triangles = new int[(bottomRing.Length - 1) * LengthY * 6];
            for (int y = 0; y < LengthY; y++)
            {
                for (int x = 0; x < (bottomRing.Length - 1); x++)
                {
                    int a = y * bottomRing.Length + x;
                    int b = a + 1;
                    int c = (y + 1) * bottomRing.Length + x;
                    int d = c + 1;
                    triangles[index] = a;
                    triangles[index + 1] = d;
                    triangles[index + 2] = b;
                    triangles[index + 3] = d;
                    triangles[index + 4] = a;
                    triangles[index + 5] = c;
                    index += 6;
                }
            }
            // Generate the UVs. Take into account the
            // cuts along the curve of the surface.
            Vector2[] uvs = new Vector2[vertices.Length];
            float uvTileScale = 1f / UVUnit;
            float fraction = 1f / (curveCuts + 1);
            for (index = 0; index < uvs.Length; index++)
                uvs[index] = new Vector2(
                    (index % bottomRing.Length) * uvTileScale * fraction,
                    vertices[index].y * uvTileScale);

            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();


            return mesh;
        }

        private Vector3[] GetRing(int detailCuts)
        {
            Vector3[] bottomRing = new Vector3[LengthX * (detailCuts + 1) + 1];
            for (int x = 0; x < bottomRing.Length; x++)
            {
                float stepAngle = Mathf.Abs(Mathf.Deg2Rad * angle)
                    * (x / (float)(bottomRing.Length - 1));
                bottomRing[x] = new Vector3(
                    radius * Mathf.Sin(stepAngle),
                    0f,
                    (radius - radius * Mathf.Cos(stepAngle)) * (angle > 0f ? 1f : -1f)
                );
            }
            return bottomRing;
        }

        protected override Vector3[][] GetWireFramePolylines()
        {
            // Setup wireframe size.
            Vector3[][] wireframe = new Vector3[LengthX + LengthY + 2][];
            int wireIndex = 0;
            // Generate a template for the bottom ring of the cylinder.
            int curveCuts = 3;
            Vector3[] bottomRing = GetRing(curveCuts);
            // Generate the vertical wires first.
            for (int x = 0; x <= LengthX; x++)
            {
                wireframe[wireIndex] = new Vector3[]
                {
                    bottomRing[x * (curveCuts + 1)],
                    bottomRing[x * (curveCuts + 1)] + Vector3.up * LengthY
                };
                wireIndex++;
            }
            // Next generate the horizontal curved
            // wires using the template base.
            wireframe[wireIndex] = bottomRing;
            wireIndex++;
            for (int y = 1; y <= LengthY; y++)
            {
                wireframe[wireIndex] = new Vector3[bottomRing.Length];
                for (int i = 0; i < bottomRing.Length; i++)
                {
                    wireframe[wireIndex][i] = bottomRing[i] +
                        Vector3.up * y;
                }
                wireIndex++;
            }
            return wireframe;
        }

        protected override bool TryLinecastLocal(Vector3 start, Vector3 end, out Vector2 hitLocation)
        {

            // Figure it whether the surface is facing outwards
            // based on both angle and flip values.
            bool expectingInwards = angle > 0f;
            // Get the center of the ring at elevation 0.
            Vector3 center = Vector3.forward * ((angle > 0f) ? 1f : -1f) * radius;
            // Get distances from the ring center.
            float radiusSquared = radius * radius;
            float startRadiusSquared = Vector2.SqrMagnitude(new Vector2(start.x, start.z) - new Vector2(center.x, center.z));
            float endRadiusSquared = Vector2.SqrMagnitude(new Vector2(end.x, end.z) - new Vector2(center.x, center.z));
            // Handle cases where the end points are both
            // either inside or outside the ring.
            if (startRadiusSquared < radiusSquared
                && endRadiusSquared < radiusSquared)
            {
                // Both point are inside. There cannot be
                // an intersection.
                hitLocation = default;
                return false;
            }
            // Use projection to truncate a line that passes
            // all the way through the ring; this will also form
            // a right triangle making the final point on the cylinder
            // easier to calculate.
            //Vector3 nearest = Vector3.Project(center - start, end - start) + start;
            Vector3 flatStart = new Vector3(start.x, 0f, start.z);
            Vector3 flatEnd = new Vector3(end.x, 0f, end.z);
            Vector3 nearest = Vector3.Project(center - flatStart, flatEnd - flatStart) + flatStart;
            nearest = Vector3.Lerp(start, end, (nearest - flatStart).magnitude / (flatEnd - flatStart).magnitude);
            float nearestRadiusSquared = Vector2.SqrMagnitude(new Vector2(nearest.x, nearest.z) - new Vector2(center.x, center.z));
            // Eliminate the edge case where the line
            // is strictly outside the ring.
            if (nearestRadiusSquared > radiusSquared)
            {
                hitLocation = default;
                return false;
            }
            else
            {
                // Choose to truncate in the direction that
                // favors the desired collision direction.
                if (expectingInwards)
                {
                    end = nearest;
                    endRadiusSquared = nearestRadiusSquared;
                }
                else
                {
                    start = nearest;
                    startRadiusSquared = nearestRadiusSquared;
                }
            }
            // We will do one last check to remove lines
            // that are going in the incorrect direction.
            if ((startRadiusSquared < radiusSquared && expectingInwards)
                || (startRadiusSquared > radiusSquared && !expectingInwards))
            {
                hitLocation = default;
                return false;
            }
            // At this point we can be sure we have a start-end
            // line with one point on the inside and one point
            // on the outside of the ring in the correct direction.
            float lineInterpolant = Mathf.Sqrt(radiusSquared - nearestRadiusSquared) / (end - start).magnitude;
            Vector3 hitPoint = Vector3.Lerp(start, end,
                (startRadiusSquared < endRadiusSquared) ?
                lineInterpolant : 1f - lineInterpolant);
            //Debug.Log(hitPoint);
            // Finally we must convert from 3D space into the relevant
            // polor coordinates that map to the cylinder.
            Vector3 circlePoint = hitPoint - center;
            float pointAngle = 90f - ((angle > 0f) ? Mathf.Atan2(-circlePoint.z, circlePoint.x)
                : Mathf.Atan2(circlePoint.z, circlePoint.x)) * Mathf.Rad2Deg;
            //Debug.Log($"Elevation = {hitPoint.y}");
            // Return the final hit point.
            hitLocation = new Vector2(pointAngle / Mathf.Abs(angle) * LengthX, hitPoint.y);
            return true;
        }

        #region Exposed Dimension Properties
        public float Radius => radius;
        public float Angle => angle;
        #endregion


        protected override sealed Vector3 GetLocationLocal(Vector2 surfaceLocation)
        {
            float angle = Mathf.Abs(Mathf.Deg2Rad * Angle * (surfaceLocation.x / LengthX));
            return new Vector3(
                Radius * Mathf.Sin(angle),
                surfaceLocation.y,
                (Radius - Radius * Mathf.Cos(angle)) * (Angle > 0f ? 1f : -1f)
            );
        }

        protected override sealed Vector3 GetOutwardsLocal(Vector2 surfaceLocation)
        {
            float currentAngle = surfaceLocation.x / LengthX * Angle * Mathf.Deg2Rad;
            return new Vector3(
                -Mathf.Sin(currentAngle),
                0f,
                Mathf.Cos(currentAngle));
        }

        protected override sealed Vector3 GetRightLocal(Vector2 surfaceLocation)
        {
            float currentAngle = surfaceLocation.x / LengthX * Angle * Mathf.Deg2Rad;
            if (Angle > 0f)
            {
                return new Vector3(
                    Mathf.Cos(currentAngle),
                    0f,
                    Mathf.Sin(currentAngle));
            }
            else
            {
                return new Vector3(
                    Mathf.Cos(currentAngle),
                    0f,
                    -Mathf.Sin(currentAngle));
            }
        }

        protected override sealed Vector3 GetUpLocal(Vector2 surfaceLocation)
        {
            return Vector3.up;
        }
    }

}
