using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MeshProcess
{
    public class ScriptVHACD
    {
        [System.Serializable]
        public unsafe struct Parameters
        {
            public static Parameters Default = new()
            {
                m_resolution = 100000,
                m_concavity = 0.001,
                m_planeDownsampling = 4,
                m_convexhullDownsampling = 4,
                m_alpha = 0.05,
                m_beta = 0.05,
                m_pca = 0,
                m_mode = 0, // 0: voxel-based (recommended), 1: tetrahedron-based
                m_maxNumVerticesPerCH = 64,
                m_minVolumePerCH = 0.0001,
                m_callback = null,
                m_logger = null,
                m_convexhullApproximation = 1,
                m_oclAcceleration = 0,
                m_maxConvexHulls = 1024,
                m_projectHullVertices = true, // This will project the output convex hull vertices onto the original source mesh to increase the floating point accuracy of the results
            };

            public static Parameters Optimal = new()
            {
                m_resolution = 100000,
                m_concavity = 0.001,
                m_planeDownsampling = 4,
                m_convexhullDownsampling = 4,
                m_alpha = 0.05,
                m_beta = 0.05,
                m_pca = 0,
                m_mode = 0, // 0: voxel-based (recommended), 1: tetrahedron-based
                m_maxNumVerticesPerCH = 64,
                m_minVolumePerCH = 0.0001,
                m_callback = null,
                m_logger = null,
                m_convexhullApproximation = 1,
                m_oclAcceleration = 0,
                m_maxConvexHulls = 4,
                m_projectHullVertices = true, // This will project the output convex hull vertices onto the original source mesh to increase the floating point accuracy of the results
            };

            public void Init()
            {
                m_resolution = 100000;
                m_concavity = 0.001;
                m_planeDownsampling = 4;
                m_convexhullDownsampling = 4;
                m_alpha = 0.05;
                m_beta = 0.05;
                m_pca = 0;
                m_mode = 0; // 0: voxel-based (recommended), 1: tetrahedron-based
                m_maxNumVerticesPerCH = 64;
                m_minVolumePerCH = 0.0001;
                m_callback = null;
                m_logger = null;
                m_convexhullApproximation = 1;
                m_oclAcceleration = 0;
                m_maxConvexHulls = 1024;
                m_projectHullVertices = true; // This will project the output convex hull vertices onto the original source mesh to increase the floating point accuracy of the results
            }

            public double m_concavity;

            public double m_alpha;

            public double m_beta;

            public double m_minVolumePerCH;

            public void* m_callback;
            public void* m_logger;

            public uint m_resolution;

            public uint m_maxNumVerticesPerCH;

            public uint m_planeDownsampling;

            public uint m_convexhullDownsampling;

            public uint m_pca;

            public uint m_mode;

            public uint m_convexhullApproximation;

            public uint m_oclAcceleration;

            public uint m_maxConvexHulls;

            public bool m_projectHullVertices;
        };

        unsafe struct ConvexHull
        {
            public double* m_points;
            public uint* m_triangles;
            public uint m_nPoints;
            public uint m_nTriangles;
            public double m_volume;
            public fixed double m_center[3];
        };

        [DllImport("libvhacd")] static extern unsafe void* CreateVHACD();

        [DllImport("libvhacd")] static extern unsafe void DestroyVHACD(void* pVHACD);

        [DllImport("libvhacd")]
        static extern unsafe bool ComputeFloat(
            void* pVHACD,
            float* points,
            uint countPoints,
            uint* triangles,
            uint countTriangles,
            Parameters* parameters);

        [DllImport("libvhacd")]
        static extern unsafe bool ComputeDouble(
            void* pVHACD,
            double* points,
            uint countPoints,
            uint* triangles,
            uint countTriangles,
            Parameters* parameters);

        [DllImport("libvhacd")] static extern unsafe uint GetNConvexHulls(void* pVHACD);

        [DllImport("libvhacd")]
        static extern unsafe void GetConvexHull(
            void* pVHACD,
            uint index,
            ConvexHull* ch);

        public Parameters m_parameters;

        public ScriptVHACD() { m_parameters.Init(); }

        public unsafe List<Mesh> GenerateConvexMeshes(Mesh mesh)
        {
            if (mesh == null)
            {
                UnityEngine.Debug.LogError("[VHACD] At `GenerateConvexMeshes`: `mesh` cannot be null!");
            }
            var vhacd = CreateVHACD();
            var parameters = m_parameters;

            var verts = mesh.vertices;
            var tris = mesh.triangles;
            fixed (Vector3* pVerts = verts)
            fixed (int* pTris = tris)
            {
                ComputeFloat(
                    vhacd,
                    (float*)pVerts, (uint)verts.Length,
                    (uint*)pTris, (uint)tris.Length / 3,
                    &parameters);
            }

            var numHulls = GetNConvexHulls(vhacd);
            List<Mesh> convexMesh = new List<Mesh>((int)numHulls);
            foreach (var index in Enumerable.Range(0, (int)numHulls))
            {
                ConvexHull hull;
                GetConvexHull(vhacd, (uint)index, &hull);

                var hullMesh = new Mesh();
                var hullVerts = new Vector3[hull.m_nPoints];
                fixed (Vector3* pHullVerts = hullVerts)
                {
                    var pComponents = hull.m_points;
                    var pVerts = pHullVerts;

                    for (var pointCount = hull.m_nPoints; pointCount != 0; --pointCount)
                    {
                        pVerts->x = (float)pComponents[0];
                        pVerts->y = (float)pComponents[1];
                        pVerts->z = (float)pComponents[2];

                        pVerts += 1;
                        pComponents += 3;
                    }
                }

                hullMesh.SetVertices(hullVerts);

                var indices = new int[hull.m_nTriangles * 3];
                Marshal.Copy((System.IntPtr)hull.m_triangles, indices, 0, indices.Length);
                hullMesh.SetTriangles(indices, 0);


                convexMesh.Add(hullMesh);
            }

            DestroyVHACD(vhacd);
            return convexMesh;
        }

        public static unsafe List<Mesh> GenerateConvexMeshes(Mesh mesh, Parameters parameters)
        {
            var vhacd = CreateVHACD();

            var verts = mesh.vertices;
            var tris = mesh.triangles;
            fixed (Vector3* pVerts = verts)
            fixed (int* pTris = tris)
            {
                ComputeFloat(
                    vhacd,
                    (float*)pVerts, (uint)verts.Length,
                    (uint*)pTris, (uint)tris.Length / 3,
                    &parameters);
            }

            var numHulls = GetNConvexHulls(vhacd);
            List<Mesh> convexMesh = new List<Mesh>((int)numHulls);
            foreach (var index in Enumerable.Range(0, (int)numHulls))
            {
                ConvexHull hull;
                GetConvexHull(vhacd, (uint)index, &hull);

                var hullMesh = new Mesh();
                var hullVerts = new Vector3[hull.m_nPoints];
                fixed (Vector3* pHullVerts = hullVerts)
                {
                    var pComponents = hull.m_points;
                    var pVerts = pHullVerts;

                    for (var pointCount = hull.m_nPoints; pointCount != 0; --pointCount)
                    {
                        pVerts->x = (float)pComponents[0];
                        pVerts->y = (float)pComponents[1];
                        pVerts->z = (float)pComponents[2];

                        pVerts += 1;
                        pComponents += 3;
                    }
                }

                hullMesh.SetVertices(hullVerts);

                var indices = new int[hull.m_nTriangles * 3];
                Marshal.Copy((System.IntPtr)hull.m_triangles, indices, 0, indices.Length);
                hullMesh.SetTriangles(indices, 0);


                convexMesh.Add(hullMesh);
            }

            DestroyVHACD(vhacd);
            return convexMesh;
        }
    }
}