using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.World.Foliage
{
    /// <summary>Camera-local meadow geometry. Only one tile builds at a time across the world.</summary>
    public sealed class MeadowTileStreamer : MonoBehaviour
    {
        private static MeadowTileStreamer activeBuilder;
        private static readonly List<MeadowTileStreamer> fields = new List<MeadowTileStreamer>();
        private static readonly System.Diagnostics.Stopwatch frameTimer = new System.Diagnostics.Stopwatch();
        private const double FrameBudgetMilliseconds = 2.0;
        internal static readonly GenerationBudget BuildBudget = new GenerationBudget((float)FrameBudgetMilliseconds);
        private Func<int, int, IEnumerator> build;
        private MeshFilter[] tiles;
        private Mesh[] meshes;
        private bool[] completed;
        private int tilesX;
        private float minX, minZ, tileSize;
        private Camera view;
        private float nextScan;
        private IEnumerator routine;
        private int buildingIndex = -1;
        private int candidate = -1;
        private float candidateDistance;

        // Load beyond the shader fade end, unload farther out to prevent churn at tile boundaries.
        private const float LoadRadius = 78f;
        private const float UnloadRadius = 94f;

        public bool IsInitialized => tiles != null;

        public void Initialize(int width, int depth, float x, float z, float size, Func<int, int, IEnumerator> builder)
        {
            tilesX = width; minX = x; minZ = z; tileSize = size; build = builder;
            tiles = new MeshFilter[width * depth];
            meshes = new Mesh[tiles.Length];
            completed = new bool[tiles.Length];
        }

        private void Update()
        {
            if (tiles == null || Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.15f;
            if (view == null || !view.isActiveAndEnabled) view = Camera.main;
            if (view == null) return;
            ScanCandidates(view.transform.position);
        }

        private void ScanCandidates(Vector3 cameraPosition)
        {
            int nearest = -1;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < tiles.Length; i++)
            {
                float dx = minX + (i % tilesX + 0.5f) * tileSize - cameraPosition.x;
                float dz = minZ + (i / tilesX + 0.5f) * tileSize - cameraPosition.z;
                float distance = dx * dx + dz * dz;
                if (completed[i] && distance > UnloadRadius * UnloadRadius)
                {
                    ReleaseTile(i);
                    completed[i] = false;
                }
                if (!completed[i] && i != buildingIndex && distance < LoadRadius * LoadRadius && distance < nearestDistance)
                {
                    nearest = i; nearestDistance = distance;
                }
            }
            candidate = nearest;
            candidateDistance = nearestDistance;
        }

        private void OnEnable()
        {
            if (!fields.Contains(this)) fields.Add(this);
        }

        private void LateUpdate()
        {
            // One coordinator selects the nearest pending tile across chunk boundaries.
            if (fields.Count == 0 || fields[0] != this) return;
            frameTimer.Restart();
            BuildBudget.Reset();
            do
            {
                if (activeBuilder == null) SelectNextTile();
                if (activeBuilder == null) return;
                var field = activeBuilder;
                try
                {
                    // A yielded tile consumed its CPU budget. Resume next frame without artificial waits.
                    if (field.routine.MoveNext()) return;
                    field.FinishBuild(true);
                    if (field.view != null) field.ScanCandidates(field.view.transform.position);
                }
                catch
                {
                    field.FinishBuild(false);
                    throw;
                }
            } while (frameTimer.Elapsed.TotalMilliseconds < FrameBudgetMilliseconds);
        }

        private static void SelectNextTile()
        {
            MeadowTileStreamer nearestField = null;
            float distance = float.MaxValue;
            foreach (var field in fields)
            {
                if (field.candidate < 0 || field.tiles == null || field.completed[field.candidate]) continue;
                if (field.candidateDistance < distance)
                {
                    nearestField = field;
                    distance = field.candidateDistance;
                }
            }
            if (nearestField == null) return;
            int index = nearestField.candidate;
            nearestField.candidate = -1;
            activeBuilder = nearestField;
            nearestField.buildingIndex = index;
            nearestField.routine = nearestField.build(index % nearestField.tilesX, index / nearestField.tilesX);
        }

        private void FinishBuild(bool succeeded)
        {
            int index = buildingIndex;
            if (index < 0) return;
            try
            {
                (routine as IDisposable)?.Dispose();
                Transform child = transform.Find($"Meadow_{index % tilesX}_{index / tilesX}");
                tiles[index] = child != null ? child.GetComponent<MeshFilter>() : null;
                meshes[index] = tiles[index] != null ? tiles[index].sharedMesh : null;
                completed[index] = succeeded || tiles[index] != null;
            }
            finally
            {
                if (activeBuilder == this) activeBuilder = null;
                buildingIndex = -1;
                routine = null;
                nextScan = 0f;
            }
        }

        private void ReleaseTile(int index)
        {
            MeshFilter filter = tiles[index];
            if (meshes[index] != null) Destroy(meshes[index]);
            meshes[index] = null;
            if (filter != null)
            {
                filter.gameObject.SetActive(false);
                Destroy(filter.gameObject);
            }
            tiles[index] = null;
        }

        private void OnDisable()
        {
            fields.Remove(this);
            candidate = -1;
            FinishBuild(false);
            if (activeBuilder == this) activeBuilder = null;
            buildingIndex = -1;
        }

        private void OnDestroy()
        {
            if (meshes == null) return;
            // Children are already being destroyed with the holder; only their owned native meshes remain.
            // Exiting Play Mode can run this callback after Application.isPlaying has become false.
            foreach (Mesh mesh in meshes)
            {
                if (mesh == null) continue;
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            build = null;
        }
    }
}
