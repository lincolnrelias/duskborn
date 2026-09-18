using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.World.Foliage
{
    /// <summary>
    /// Utilitário procedural para geração de malhas low-poly de folhagem estilizada
    /// (tufo de grama com lâminas curvadas, arbustos em esferas agrupadas com normais esféricas anime,
    /// e copas de árvores estilizadas com vertex colors e pesos de vento).
    /// </summary>
    public static class FoliageMeshUtility
    {
        /// <summary>
        /// Gera uma malha de tufo de grama estilizado com múltiplas lâminas curvadas.
        /// Base (UV.y=0, Alpha=0) não sofre vento.
        /// Ponta (UV.y=1, Alpha=1) oscila suavemente com o vento.
        /// </summary>
        public static Mesh CreateGrassTuftMesh(int bladeCount = 5, float height = 0.95f, float baseWidth = 0.18f, float tipWidth = 0.02f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_GrassTuft_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            float angleStep = 360f / bladeCount;

            for (int i = 0; i < bladeCount; i++)
            {
                float baseAngle = i * angleStep + Random.Range(-12f, 12f);
                float rad = baseAngle * Mathf.Deg2Rad;
                Vector3 bladeDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 bladeSide = new Vector3(-bladeDir.z, 0f, bladeDir.x);

                float bladeHeight = height * Random.Range(0.85f, 1.18f);
                float bladeLean = Random.Range(0.18f, 0.38f);
                Vector3 curveOffset = bladeDir * bladeLean;

                int baseIndex = verts.Count;

                // Segmento 0: Base no solo (sem vento)
                Vector3 v0_left = -bladeSide * (baseWidth * 0.5f);
                Vector3 v0_right = bladeSide * (baseWidth * 0.5f);

                // Segmento 1: Meio da lâmina com curvatura
                float midY = bladeHeight * 0.52f;
                Vector3 v1_left = -bladeSide * (baseWidth * 0.38f) + Vector3.up * midY + curveOffset * 0.45f;
                Vector3 v1_right = bladeSide * (baseWidth * 0.38f) + Vector3.up * midY + curveOffset * 0.45f;

                // Segmento 2: Ponta da lâmina
                Vector3 v2_tip = Vector3.up * bladeHeight + curveOffset;

                // Normais apontando predominantemente para cima para iluminação uniforme contínua (estilo Genshin / Zelda)
                Vector3 nBase = (Vector3.up * 0.94f + bladeDir * 0.06f).normalized;
                Vector3 nMid = (Vector3.up * 0.97f + bladeDir * 0.03f).normalized;
                Vector3 nTip = Vector3.up;

                // Adiciona vértices
                verts.Add(v0_left);  // 0
                verts.Add(v0_right); // 1
                verts.Add(v1_left);  // 2
                verts.Add(v1_right); // 3
                verts.Add(v2_tip);   // 4

                normals.Add(nBase);
                normals.Add(nBase);
                normals.Add(nMid);
                normals.Add(nMid);
                normals.Add(nTip);

                // UVs: x = horizontal (0 a 1), y = altura vertical (0 no solo a 1 no topo)
                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(1.0f, 0.0f));
                uvs.Add(new Vector2(0.15f, 0.52f));
                uvs.Add(new Vector2(0.85f, 0.52f));
                uvs.Add(new Vector2(0.5f, 1.0f));

                // Cores de vértice: RGB = branco (multiplicado pelo shader), Alpha = peso de deslocamento do vento
                colors.Add(new Color(1f, 1f, 1f, 0.0f)); // Solo estático
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.45f)); // Meio
                colors.Add(new Color(1f, 1f, 1f, 0.45f));
                colors.Add(new Color(1f, 1f, 1f, 1.0f));  // Ponta com balanço total

                // Triângulos (Front e Back para garantir renderização perfeita)
                // Quad inferior (0, 2, 1) e (1, 2, 3)
                tris.Add(baseIndex + 0);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 1);

                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 3);

                // Triângulo superior até a ponta (2, 4, 3)
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 4);
                tris.Add(baseIndex + 3);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de carpete denso contínuo (Dense Carpet Grass), projetada especificamente
        /// para criar o efeito de grama pintada (Unity Terrain Detail Painter / Genshin Impact).
        /// As lâminas são distribuídas organicamente sobre um disco de raio amplo em espiral áurea,
        /// eliminando o centro único (efeito abacaxi/tufo isolado) e fundindo-se em um tapete contínuo.
        /// </summary>
        public static Mesh CreateDenseCarpetMesh(int bladeCount = 18, float radius = 0.85f, float height = 0.90f, float baseWidth = 0.28f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_DenseCarpet_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            const float goldenAngle = 2.39996323f; // ~137.51 graus em radianos

            for (int i = 0; i < bladeCount; i++)
            {
                // Distribuição áurea de raízes: cobre uniformemente o disco sem agrupar no centro
                float rNorm = Mathf.Sqrt((i + 0.5f) / (float)bladeCount);
                float r = rNorm * radius;
                float theta = i * goldenAngle;
                Vector3 rootPos = new Vector3(Mathf.Cos(theta) * r, 0f, Mathf.Sin(theta) * r);

                float bladeAngle = theta + Random.Range(-0.45f, 0.45f);
                Vector3 bladeDir = new Vector3(Mathf.Cos(bladeAngle), 0f, Mathf.Sin(bladeAngle));
                Vector3 bladeSide = new Vector3(-bladeDir.z, 0f, bladeDir.x);

                bool isOuter = (rNorm > 0.65f);
                float bladeHeight = isOuter
                    ? height * Random.Range(0.78f, 0.92f)
                    : height * Random.Range(0.95f, 1.15f);

                float width = isOuter ? baseWidth * 1.15f : baseWidth * 0.92f;
                float lean = isOuter ? Random.Range(0.32f, 0.52f) : Random.Range(0.18f, 0.35f);
                Vector3 curveOffset = bladeDir * lean;

                int baseIndex = verts.Count;

                Vector3 v0_left = rootPos - bladeSide * (width * 0.5f);
                Vector3 v0_right = rootPos + bladeSide * (width * 0.5f);

                float midY = bladeHeight * 0.48f;
                Vector3 v1_left = rootPos - bladeSide * (width * 0.38f) + Vector3.up * midY + curveOffset * 0.45f;
                Vector3 v1_right = rootPos + bladeSide * (width * 0.38f) + Vector3.up * midY + curveOffset * 0.45f;

                Vector3 v2_tip = rootPos + Vector3.up * bladeHeight + curveOffset;

                Vector3 nBase = (Vector3.up * 0.95f + bladeDir * 0.05f).normalized;
                Vector3 nMid = (Vector3.up * 0.97f + bladeDir * 0.03f).normalized;
                Vector3 nTip = Vector3.up;

                verts.Add(v0_left);
                verts.Add(v0_right);
                verts.Add(v1_left);
                verts.Add(v1_right);
                verts.Add(v2_tip);

                normals.Add(nBase);
                normals.Add(nBase);
                normals.Add(nMid);
                normals.Add(nMid);
                normals.Add(nTip);

                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(1.0f, 0.0f));
                uvs.Add(new Vector2(0.12f, 0.48f));
                uvs.Add(new Vector2(0.88f, 0.48f));
                uvs.Add(new Vector2(0.5f, 1.0f));

                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, isOuter ? 0.35f : 0.45f));
                colors.Add(new Color(1f, 1f, 1f, isOuter ? 0.35f : 0.45f));
                colors.Add(new Color(1f, 1f, 1f, isOuter ? 0.75f : 1.0f));

                tris.Add(baseIndex + 0);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 1);

                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 3);

                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 4);
                tris.Add(baseIndex + 3);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de grama alta e viçosa (Lush Grass Clump), com lâminas espaçadas
        /// em espiral áurea cobrindo uma área de 1.5m de diâmetro para conferir volume e maciez natural.
        /// </summary>
        public static Mesh CreateLushGrassClumpMesh(int bladeCount = 15, float radius = 0.75f, float height = 1.30f, float baseWidth = 0.22f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_LushGrassClump_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            const float goldenAngle = 2.39996323f;

            for (int i = 0; i < bladeCount; i++)
            {
                float rNorm = Mathf.Sqrt((i + 0.5f) / (float)bladeCount);
                float r = rNorm * radius;
                float theta = i * goldenAngle;
                Vector3 rootPos = new Vector3(Mathf.Cos(theta) * r, 0f, Mathf.Sin(theta) * r);

                bool isCenterBlade = (rNorm < 0.45f);
                float bladeHeight = isCenterBlade
                    ? height * Random.Range(1.05f, 1.25f)
                    : height * Random.Range(0.75f, 0.95f);

                float width = isCenterBlade ? baseWidth * 1.15f : baseWidth * 0.88f;
                float bladeAngle = theta + Random.Range(-0.40f, 0.40f);
                Vector3 bladeDir = new Vector3(Mathf.Cos(bladeAngle), 0f, Mathf.Sin(bladeAngle));
                Vector3 bladeSide = new Vector3(-bladeDir.z, 0f, bladeDir.x);

                float lean = isCenterBlade ? Random.Range(0.12f, 0.28f) : Random.Range(0.35f, 0.55f);
                Vector3 curveOffset = bladeDir * lean;

                int baseIndex = verts.Count;

                Vector3 v0_left = rootPos - bladeSide * (width * 0.5f);
                Vector3 v0_right = rootPos + bladeSide * (width * 0.5f);

                float midY = bladeHeight * 0.50f;
                Vector3 v1_left = rootPos - bladeSide * (width * 0.38f) + Vector3.up * midY + curveOffset * 0.40f;
                Vector3 v1_right = rootPos + bladeSide * (width * 0.38f) + Vector3.up * midY + curveOffset * 0.40f;

                Vector3 v2_tip = rootPos + Vector3.up * bladeHeight + curveOffset;

                Vector3 nBase = (Vector3.up * 0.95f + bladeDir * 0.05f).normalized;
                Vector3 nMid = (Vector3.up * 0.97f + bladeDir * 0.03f).normalized;
                Vector3 nTip = Vector3.up;

                verts.Add(v0_left);
                verts.Add(v0_right);
                verts.Add(v1_left);
                verts.Add(v1_right);
                verts.Add(v2_tip);

                normals.Add(nBase);
                normals.Add(nBase);
                normals.Add(nMid);
                normals.Add(nMid);
                normals.Add(nTip);

                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(1.0f, 0.0f));
                uvs.Add(new Vector2(0.15f, 0.50f));
                uvs.Add(new Vector2(0.85f, 0.50f));
                uvs.Add(new Vector2(0.5f, 1.0f));

                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.45f));
                colors.Add(new Color(1f, 1f, 1f, 0.45f));
                colors.Add(new Color(1f, 1f, 1f, 1.0f));

                tris.Add(baseIndex + 0);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 1);

                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 3);

                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 4);
                tris.Add(baseIndex + 3);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de grama fina de pradaria (Prairie Grass), distribuída em um disco de 1.1m
        /// ideal para orlas de campos, transições de trilhas e bordas de clareiras.
        /// </summary>
        public static Mesh CreatePrairieGrassMesh(int bladeCount = 10, float radius = 0.55f, float height = 0.70f, float baseWidth = 0.16f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_PrairieGrass_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            const float goldenAngle = 2.39996323f;

            for (int i = 0; i < bladeCount; i++)
            {
                float rNorm = Mathf.Sqrt((i + 0.5f) / (float)bladeCount);
                float r = rNorm * radius;
                float theta = i * goldenAngle;
                Vector3 rootPos = new Vector3(Mathf.Cos(theta) * r, 0f, Mathf.Sin(theta) * r);

                float bladeAngle = theta + Random.Range(-0.45f, 0.45f);
                Vector3 bladeDir = new Vector3(Mathf.Cos(bladeAngle), 0f, Mathf.Sin(bladeAngle));
                Vector3 bladeSide = new Vector3(-bladeDir.z, 0f, bladeDir.x);

                float bladeH = height * Random.Range(0.85f, 1.15f);
                float lean = Random.Range(0.22f, 0.42f);
                Vector3 curveOffset = bladeDir * lean;

                int baseIndex = verts.Count;

                Vector3 v0_left = rootPos - bladeSide * (baseWidth * 0.5f);
                Vector3 v0_right = rootPos + bladeSide * (baseWidth * 0.5f);

                float midY = bladeH * 0.52f;
                Vector3 v1_left = rootPos - bladeSide * (baseWidth * 0.35f) + Vector3.up * midY + curveOffset * 0.42f;
                Vector3 v1_right = rootPos + bladeSide * (baseWidth * 0.35f) + Vector3.up * midY + curveOffset * 0.42f;

                Vector3 v2_tip = rootPos + Vector3.up * bladeH + curveOffset;

                Vector3 nBase = (Vector3.up * 0.95f + bladeDir * 0.05f).normalized;
                Vector3 nMid = (Vector3.up * 0.97f + bladeDir * 0.03f).normalized;
                Vector3 nTip = Vector3.up;

                verts.Add(v0_left);
                verts.Add(v0_right);
                verts.Add(v1_left);
                verts.Add(v1_right);
                verts.Add(v2_tip);

                normals.Add(nBase);
                normals.Add(nBase);
                normals.Add(nMid);
                normals.Add(nMid);
                normals.Add(nTip);

                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(1.0f, 0.0f));
                uvs.Add(new Vector2(0.15f, 0.52f));
                uvs.Add(new Vector2(0.85f, 0.52f));
                uvs.Add(new Vector2(0.5f, 1.0f));

                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.40f));
                colors.Add(new Color(1f, 1f, 1f, 0.40f));
                colors.Add(new Color(1f, 1f, 1f, 0.85f));

                tris.Add(baseIndex + 0);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 1);

                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 3);

                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 4);
                tris.Add(baseIndex + 3);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de juncos / capim alto de várzea (Reed Grass),
        /// com lâminas esguias e verticais ideais para margens de corpos d'água e depressões úmidas de vales.
        /// </summary>
        public static Mesh CreateReedGrassMesh(int bladeCount = 6, float height = 1.45f, float baseWidth = 0.12f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_ReedGrass_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            float angleStep = 360f / bladeCount;

            for (int i = 0; i < bladeCount; i++)
            {
                float bladeAngle = i * angleStep + Random.Range(-12f, 12f);
                float rad = bladeAngle * Mathf.Deg2Rad;
                Vector3 bladeDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 bladeSide = new Vector3(-bladeDir.z, 0f, bladeDir.x);

                float bladeH = height * Random.Range(0.90f, 1.20f);
                float lean = Random.Range(0.12f, 0.22f);
                Vector3 curveOffset = bladeDir * lean;

                int baseIndex = verts.Count;

                Vector3 v0_left = -bladeSide * (baseWidth * 0.5f);
                Vector3 v0_right = bladeSide * (baseWidth * 0.5f);

                float midY = bladeH * 0.55f;
                Vector3 v1_left = -bladeSide * (baseWidth * 0.32f) + Vector3.up * midY + curveOffset * 0.35f;
                Vector3 v1_right = bladeSide * (baseWidth * 0.32f) + Vector3.up * midY + curveOffset * 0.35f;

                Vector3 v2_tip = Vector3.up * bladeH + curveOffset;

                Vector3 nBase = (Vector3.up * 0.94f + bladeDir * 0.06f).normalized;
                Vector3 nMid = (Vector3.up * 0.97f + bladeDir * 0.03f).normalized;
                Vector3 nTip = Vector3.up;

                verts.Add(v0_left);
                verts.Add(v0_right);
                verts.Add(v1_left);
                verts.Add(v1_right);
                verts.Add(v2_tip);

                normals.Add(nBase);
                normals.Add(nBase);
                normals.Add(nMid);
                normals.Add(nMid);
                normals.Add(nTip);

                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(1.0f, 0.0f));
                uvs.Add(new Vector2(0.15f, 0.55f));
                uvs.Add(new Vector2(0.85f, 0.55f));
                uvs.Add(new Vector2(0.5f, 1.0f));

                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.35f));
                colors.Add(new Color(1f, 1f, 1f, 0.35f));
                colors.Add(new Color(1f, 1f, 1f, 1.0f));

                tris.Add(baseIndex + 0);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 1);

                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 3);

                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 4);
                tris.Add(baseIndex + 3);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de tufo de grama com flores silvestres elevadas (Wildflowers).
        /// As hastes e lâminas usam UV.x em [0,1], enquanto as pétalas/cabeça da flor
        /// são codificadas com UV.x >= 2.0f para permitir colorização vibrante de pétalas.
        /// As lâminas e flores são distribuídas em um disco para se fundir naturalmente com o carpete ao redor.
        /// </summary>
        public static Mesh CreateWildflowerTuftMesh(int bladeCount = 14, int flowerCount = 4, float radius = 0.75f, float height = 0.90f, float flowerHeight = 1.15f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_WildflowerTuft_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            const float goldenAngle = 2.39996323f;

            for (int i = 0; i < bladeCount; i++)
            {
                float rNorm = Mathf.Sqrt((i + 0.5f) / (float)bladeCount);
                float r = rNorm * radius;
                float theta = i * goldenAngle;
                Vector3 rootPos = new Vector3(Mathf.Cos(theta) * r, 0f, Mathf.Sin(theta) * r);

                float bladeAngle = theta + Random.Range(-0.40f, 0.40f);
                Vector3 bladeDir = new Vector3(Mathf.Cos(bladeAngle), 0f, Mathf.Sin(bladeAngle));
                Vector3 bladeSide = new Vector3(-bladeDir.z, 0f, bladeDir.x);

                float bladeH = height * Random.Range(0.80f, 1.05f);
                float lean = Random.Range(0.20f, 0.35f);
                Vector3 curveOffset = bladeDir * lean;

                int baseIndex = verts.Count;
                float width = 0.22f;

                Vector3 v0_left = rootPos - bladeSide * (width * 0.5f);
                Vector3 v0_right = rootPos + bladeSide * (width * 0.5f);
                float midY = bladeH * 0.50f;
                Vector3 v1_left = rootPos - bladeSide * (width * 0.35f) + Vector3.up * midY + curveOffset * 0.45f;
                Vector3 v1_right = rootPos + bladeSide * (width * 0.35f) + Vector3.up * midY + curveOffset * 0.45f;
                Vector3 v2_tip = rootPos + Vector3.up * bladeH + curveOffset;

                Vector3 nBase = (Vector3.up * 0.95f + bladeDir * 0.05f).normalized;
                Vector3 nMid = (Vector3.up * 0.97f + bladeDir * 0.03f).normalized;
                Vector3 nTip = Vector3.up;

                verts.Add(v0_left);
                verts.Add(v0_right);
                verts.Add(v1_left);
                verts.Add(v1_right);
                verts.Add(v2_tip);

                normals.Add(nBase);
                normals.Add(nBase);
                normals.Add(nMid);
                normals.Add(nMid);
                normals.Add(nTip);

                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(1.0f, 0.0f));
                uvs.Add(new Vector2(0.15f, 0.50f));
                uvs.Add(new Vector2(0.85f, 0.50f));
                uvs.Add(new Vector2(0.5f, 1.0f));

                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.40f));
                colors.Add(new Color(1f, 1f, 1f, 0.40f));
                colors.Add(new Color(1f, 1f, 1f, 1.0f));

                tris.Add(baseIndex + 0);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 3);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 4);
                tris.Add(baseIndex + 3);
            }

            float flowerAngleStep = 360f / flowerCount;
            for (int f = 0; f < flowerCount; f++)
            {
                float flowerAngle = f * flowerAngleStep + Random.Range(-20f, 20f);
                float rad = flowerAngle * Mathf.Deg2Rad;
                float stemDist = Random.Range(0.15f, radius * 0.70f);
                Vector3 stemOrigin = new Vector3(Mathf.Cos(rad) * stemDist, 0f, Mathf.Sin(rad) * stemDist);

                float actualFlowerH = flowerHeight * Random.Range(0.88f, 1.18f);
                Vector3 stemTop = stemOrigin + new Vector3(Mathf.Cos(rad) * 0.12f, actualFlowerH, Mathf.Sin(rad) * 0.12f);

                int stemIndex = verts.Count;
                float stemW = 0.035f;
                Vector3 stemSide = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * (stemW * 0.5f);

                verts.Add(stemOrigin - stemSide);
                verts.Add(stemOrigin + stemSide);
                verts.Add(stemTop - stemSide);
                verts.Add(stemTop + stemSide);

                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);

                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(1.0f, 0.0f));
                uvs.Add(new Vector2(0.0f, 0.95f));
                uvs.Add(new Vector2(1.0f, 0.95f));

                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.90f));
                colors.Add(new Color(1f, 1f, 1f, 0.90f));

                tris.Add(stemIndex + 0);
                tris.Add(stemIndex + 2);
                tris.Add(stemIndex + 1);
                tris.Add(stemIndex + 1);
                tris.Add(stemIndex + 2);
                tris.Add(stemIndex + 3);

                int petalIndex = verts.Count;
                float petalRadius = Random.Range(0.09f, 0.14f);

                Vector3 pDir1 = new Vector3(Mathf.Cos(rad + 0.5f), 0.15f, Mathf.Sin(rad + 0.5f)).normalized * petalRadius;
                Vector3 pSide1 = new Vector3(-pDir1.z, 0f, pDir1.x).normalized * (petalRadius * 0.85f);

                Vector3 pDir2 = pSide1;
                Vector3 pSide2 = -pDir1;

                verts.Add(stemTop - pDir1 - pSide1 * 0.5f);
                verts.Add(stemTop + pDir1 - pSide1 * 0.5f);
                verts.Add(stemTop - pDir1 + pSide1 * 0.5f);
                verts.Add(stemTop + pDir1 + pSide1 * 0.5f);

                verts.Add(stemTop - pDir2 - pSide2 * 0.5f);
                verts.Add(stemTop + pDir2 - pSide2 * 0.5f);
                verts.Add(stemTop - pDir2 + pSide2 * 0.5f);
                verts.Add(stemTop + pDir2 + pSide2 * 0.5f);

                for (int p = 0; p < 8; p++)
                {
                    normals.Add(Vector3.up);
                    uvs.Add(new Vector2(2.5f, 1.0f));
                    colors.Add(new Color(1f, 1f, 1f, 1.0f));
                }

                tris.Add(petalIndex + 0);
                tris.Add(petalIndex + 2);
                tris.Add(petalIndex + 1);
                tris.Add(petalIndex + 1);
                tris.Add(petalIndex + 2);
                tris.Add(petalIndex + 3);

                tris.Add(petalIndex + 4);
                tris.Add(petalIndex + 6);
                tris.Add(petalIndex + 5);
                tris.Add(petalIndex + 5);
                tris.Add(petalIndex + 6);
                tris.Add(petalIndex + 7);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de arbusto low-poly composta por agrupamentos esféricos (Puffs)
        /// com normais esféricas anime voltadas para fora do centro do tufo, produzindo sombreamento suave e volumoso.
        /// </summary>
        public static Mesh CreateBushMesh(int lobes = 4, float baseRadius = 0.65f, float height = 1.0f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_Bush_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            // Centros dos agrupamentos/lobos
            Vector3[] lobeCenters = new Vector3[lobes];
            float[] lobeRadii = new float[lobes];

            lobeCenters[0] = new Vector3(0f, height * 0.48f, 0f);
            lobeRadii[0] = baseRadius * 0.95f;

            for (int l = 1; l < lobes; l++)
            {
                float angle = (l - 1) * (360f / (lobes - 1)) + 25f;
                float rad = angle * Mathf.Deg2Rad;
                float dist = baseRadius * 0.45f;
                lobeCenters[l] = new Vector3(Mathf.Cos(rad) * dist, height * 0.40f + (l % 2 == 0 ? 0.12f : -0.05f), Mathf.Sin(rad) * dist);
                lobeRadii[l] = baseRadius * Random.Range(0.68f, 0.85f);
            }

            // Gera geometria icosaédrica subdividida leve para cada lobo
            for (int l = 0; l < lobes; l++)
            {
                Vector3 center = lobeCenters[l];
                float radius = lobeRadii[l];
                int startV = verts.Count;

                // Esfera low-poly simplificada (lat/long com 6 segmentos e 5 anéis)
                int rings = 4;
                int segments = 6;

                for (int r = 0; r <= rings; r++)
                {
                    float v = (float)r / rings;
                    float phi = v * Mathf.PI; // 0 a PI

                    for (int s = 0; s <= segments; s++)
                    {
                        float u = (float)s / segments;
                        float theta = u * Mathf.PI * 2.0f;

                        Vector3 spherePos = new Vector3(
                            Mathf.Sin(phi) * Mathf.Cos(theta),
                            Mathf.Cos(phi),
                            Mathf.Sin(phi) * Mathf.Sin(theta)
                        );

                        Vector3 vertPos = center + spherePos * radius;
                        // Achata a base do arbusto em contato com o solo
                        if (vertPos.y < 0.05f) vertPos.y = 0.02f;

                        // Técnica Anime Ghibli: Normal radial a partir do centro do lobo ou centro do arbusto
                        Vector3 animeNormal = (vertPos - new Vector3(0f, height * 0.35f, 0f)).normalized;

                        verts.Add(vertPos);
                        normals.Add(animeNormal);

                        float vertH = Mathf.Clamp01(vertPos.y / height);
                        uvs.Add(new Vector2(u, vertH));

                        // Base estática, topo e laterais oscilando
                        float windStrength = Mathf.Clamp01((vertPos.y - 0.12f) / height) * 0.65f;
                        colors.Add(new Color(1f, 1f, 1f, windStrength));
                    }
                }

                int stride = segments + 1;
                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int current = startV + r * stride + s;
                        int next = current + stride;

                        tris.Add(current);
                        tris.Add(next);
                        tris.Add(next + 1);

                        tris.Add(current);
                        tris.Add(next + 1);
                        tris.Add(current + 1);
                    }
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de arbusto florido / frutífero (Flowering Bush),
        /// combinando a copa volumosa anime com botões coloridos de flores/bagas distribuídos na superfície.
        /// Os botões de flor são marcados com UV.x >= 2.0f para receber a coloração vibrante de pétalas.
        /// </summary>
        public static Mesh CreateFloweringBushMesh(int lobes = 4, int flowerCount = 10, float baseRadius = 0.65f, float height = 1.0f)
        {
            Mesh mesh = CreateBushMesh(lobes, baseRadius, height);
            mesh.name = "Mesh_FloweringBush_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            mesh.GetVertices(verts);
            mesh.GetNormals(normals);
            mesh.GetUVs(0, uvs);
            mesh.GetColors(colors);
            mesh.GetTriangles(tris, 0);

            float goldenAngle = 137.5f * Mathf.Deg2Rad;

            for (int f = 0; f < flowerCount; f++)
            {
                float theta = f * goldenAngle;
                float yFrac = 0.35f + (float)f / flowerCount * 0.55f;
                float rHorizontal = baseRadius * Mathf.Sqrt(1f - (yFrac - 0.5f) * (yFrac - 0.5f) * 4f) * Random.Range(0.85f, 1.08f);

                Vector3 flowerCenter = new Vector3(
                    Mathf.Cos(theta) * rHorizontal,
                    yFrac * height,
                    Mathf.Sin(theta) * rHorizontal
                );

                Vector3 outward = (flowerCenter - new Vector3(0f, height * 0.40f, 0f)).normalized;
                Vector3 flowerSide = Vector3.Cross(outward, Vector3.up).normalized;
                if (flowerSide == Vector3.zero) flowerSide = Vector3.right;
                Vector3 flowerUp = Vector3.Cross(flowerSide, outward).normalized;

                float budSize = Random.Range(0.08f, 0.13f);
                int budIndex = verts.Count;

                Vector3 b0 = flowerCenter - flowerSide * (budSize * 0.5f) - flowerUp * (budSize * 0.5f);
                Vector3 b1 = flowerCenter + flowerSide * (budSize * 0.5f) - flowerUp * (budSize * 0.5f);
                Vector3 b2 = flowerCenter - flowerSide * (budSize * 0.5f) + flowerUp * (budSize * 0.5f);
                Vector3 b3 = flowerCenter + flowerSide * (budSize * 0.5f) + flowerUp * (budSize * 0.5f);

                verts.Add(b0);
                verts.Add(b1);
                verts.Add(b2);
                verts.Add(b3);

                normals.Add(outward);
                normals.Add(outward);
                normals.Add(outward);
                normals.Add(outward);

                // UV.x >= 2.0f indica vértice de pétala/flor
                uvs.Add(new Vector2(2.5f, 1.0f));
                uvs.Add(new Vector2(2.5f, 1.0f));
                uvs.Add(new Vector2(2.5f, 1.0f));
                uvs.Add(new Vector2(2.5f, 1.0f));

                float wind = Mathf.Clamp01((flowerCenter.y - 0.12f) / height) * 0.65f;
                colors.Add(new Color(1f, 1f, 1f, wind));
                colors.Add(new Color(1f, 1f, 1f, wind));
                colors.Add(new Color(1f, 1f, 1f, wind));
                colors.Add(new Color(1f, 1f, 1f, wind));

                tris.Add(budIndex + 0);
                tris.Add(budIndex + 2);
                tris.Add(budIndex + 1);

                tris.Add(budIndex + 1);
                tris.Add(budIndex + 2);
                tris.Add(budIndex + 3);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de arbusto rasteiro de cobertura de solo (Ground Shrub),
        /// mais baixo e achatado horizontalmente, ideal para ancorar rochas e transições.
        /// </summary>
        public static Mesh CreateGroundShrubMesh(int lobes = 5, float radius = 0.95f, float height = 0.45f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_GroundShrub_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            Vector3[] lobeCenters = new Vector3[lobes];
            float[] lobeRadii = new float[lobes];

            lobeCenters[0] = new Vector3(0f, height * 0.50f, 0f);
            lobeRadii[0] = radius * 0.65f;

            float angleStep = 360f / (lobes - 1);
            for (int l = 1; l < lobes; l++)
            {
                float rad = ((l - 1) * angleStep + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                float dist = radius * 0.50f;
                lobeCenters[l] = new Vector3(Mathf.Cos(rad) * dist, height * 0.40f, Mathf.Sin(rad) * dist);
                lobeRadii[l] = radius * Random.Range(0.48f, 0.62f);
            }

            for (int l = 0; l < lobes; l++)
            {
                Vector3 center = lobeCenters[l];
                float rLobe = lobeRadii[l];
                int startV = verts.Count;

                int rings = 3;
                int segments = 5;

                for (int r = 0; r <= rings; r++)
                {
                    float v = (float)r / rings;
                    float phi = v * Mathf.PI;

                    for (int s = 0; s <= segments; s++)
                    {
                        float u = (float)s / segments;
                        float theta = u * Mathf.PI * 2.0f;

                        Vector3 spherePos = new Vector3(
                            Mathf.Sin(phi) * Mathf.Cos(theta) * 1.2f, // Mais achatado horizontalmente
                            Mathf.Cos(phi) * 0.70f,
                            Mathf.Sin(phi) * Mathf.Sin(theta) * 1.2f
                        );

                        Vector3 vertPos = center + spherePos * rLobe;
                        if (vertPos.y < 0.03f) vertPos.y = 0.02f;

                        Vector3 animeNormal = (vertPos - new Vector3(0f, height * 0.20f, 0f)).normalized;

                        verts.Add(vertPos);
                        normals.Add(animeNormal);

                        float vertH = Mathf.Clamp01(vertPos.y / height);
                        uvs.Add(new Vector2(u, vertH));

                        float windStrength = Mathf.Clamp01((vertPos.y - 0.08f) / height) * 0.40f;
                        colors.Add(new Color(1f, 1f, 1f, windStrength));
                    }
                }

                int stride = segments + 1;
                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int current = startV + r * stride + s;
                        int next = current + stride;

                        tris.Add(current);
                        tris.Add(next);
                        tris.Add(next + 1);

                        tris.Add(current);
                        tris.Add(next + 1);
                        tris.Add(current + 1);
                    }
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de samambaia / arbusto rasteiro estilizado (Fern Bush),
        /// com folhas arqueadas em leque radial voltadas para fora.
        /// Ideal para margens de florestas, sob copas de árvores e ao redor de rochas.
        /// </summary>
        public static Mesh CreateFernBushMesh(int frondCount = 7, float radius = 0.85f, float height = 0.65f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_FernBush_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            float angleStep = 360f / frondCount;

            for (int f = 0; f < frondCount; f++)
            {
                float angle = f * angleStep + Random.Range(-12f, 12f);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 frondDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 frondSide = new Vector3(-frondDir.z, 0f, frondDir.x);

                float frondLen = radius * Random.Range(0.85f, 1.15f);
                float frondH = height * Random.Range(0.80f, 1.10f);

                int baseIndex = verts.Count;
                float baseW = 0.12f;
                float midW = 0.26f;

                Vector3 v0_l = -frondSide * (baseW * 0.5f);
                Vector3 v0_r = frondSide * (baseW * 0.5f);

                Vector3 midPos = frondDir * (frondLen * 0.55f) + Vector3.up * (frondH * 0.95f);
                Vector3 v1_l = midPos - frondSide * (midW * 0.5f);
                Vector3 v1_r = midPos + frondSide * (midW * 0.5f);

                Vector3 v2_tip = frondDir * frondLen + Vector3.up * (frondH * 0.35f);

                Vector3 nBase = (Vector3.up * 0.6f + frondDir * 0.4f).normalized;
                Vector3 nMid = (Vector3.up * 0.75f + frondDir * 0.35f).normalized;
                Vector3 nTip = (Vector3.up * 0.4f + frondDir * 0.6f).normalized;

                verts.Add(v0_l);
                verts.Add(v0_r);
                verts.Add(v1_l);
                verts.Add(v1_r);
                verts.Add(v2_tip);

                normals.Add(nBase);
                normals.Add(nBase);
                normals.Add(nMid);
                normals.Add(nMid);
                normals.Add(nTip);

                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(1.0f, 0.0f));
                uvs.Add(new Vector2(0.1f, 0.55f));
                uvs.Add(new Vector2(0.9f, 0.55f));
                uvs.Add(new Vector2(0.5f, 1.0f));

                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.40f));
                colors.Add(new Color(1f, 1f, 1f, 0.40f));
                colors.Add(new Color(1f, 1f, 1f, 0.80f));

                tris.Add(baseIndex + 0);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 1);

                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 3);

                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 4);
                tris.Add(baseIndex + 3);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de copa de árvore estilizada com lóbulos volumosos
        /// e gradiente vertical de vento e cores.
        /// </summary>
        public static Mesh CreateTreeCanopyMesh(float radius = 2.2f, float height = 3.5f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_TreeCanopy_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            Vector3[] lobes = new Vector3[]
            {
                new Vector3(0f, height * 0.62f, 0f),
                new Vector3(radius * 0.42f, height * 0.45f, radius * 0.25f),
                new Vector3(-radius * 0.38f, height * 0.48f, radius * 0.35f),
                new Vector3(0.08f, height * 0.42f, -radius * 0.45f),
                new Vector3(-radius * 0.25f, height * 0.72f, -radius * 0.20f)
            };

            float[] lobeRadii = new float[]
            {
                radius * 0.82f,
                radius * 0.65f,
                radius * 0.62f,
                radius * 0.68f,
                radius * 0.58f
            };

            for (int l = 0; l < lobes.Length; l++)
            {
                Vector3 center = lobes[l];
                float rLobe = lobeRadii[l];
                int startV = verts.Count;

                int rings = 5;
                int segments = 7;

                for (int r = 0; r <= rings; r++)
                {
                    float v = (float)r / rings;
                    float phi = v * Mathf.PI;

                    for (int s = 0; s <= segments; s++)
                    {
                        float u = (float)s / segments;
                        float theta = u * Mathf.PI * 2.0f;

                        Vector3 spherePos = new Vector3(
                            Mathf.Sin(phi) * Mathf.Cos(theta),
                            Mathf.Cos(phi) * 0.88f, // Ligeiramente achatado no topo
                            Mathf.Sin(phi) * Mathf.Sin(theta)
                        );

                        Vector3 vertPos = center + spherePos * rLobe;
                        Vector3 animeNormal = (vertPos - new Vector3(0f, height * 0.5f, 0f)).normalized;

                        verts.Add(vertPos);
                        normals.Add(animeNormal);

                        float vertH = Mathf.Clamp01(vertPos.y / height);
                        uvs.Add(new Vector2(u, vertH));

                        float windWeight = Mathf.Clamp01(vertH * 0.85f);
                        colors.Add(new Color(1f, 1f, 1f, windWeight));
                    }
                }

                int stride = segments + 1;
                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int current = startV + r * stride + s;
                        int next = current + stride;

                        tris.Add(current);
                        tris.Add(next);
                        tris.Add(next + 1);

                        tris.Add(current);
                        tris.Add(next + 1);
                        tris.Add(current + 1);
                    }
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        #region Little Rocks & Pebbles (Low-Poly Static Props)

        /// <summary>
        /// Utilitário para adicionar uma face triangular plana com normais calculadas por produto vetorial.
        /// </summary>
        private static void AddFlatFace(
            Vector3 a, Vector3 b, Vector3 c,
            List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<Color> colors, List<int> tris,
            float alpha = 0.0f,
            Vector2? uvA = null, Vector2? uvB = null, Vector2? uvC = null)
        {
            int start = verts.Count;
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(normal); normals.Add(normal); normals.Add(normal);
            uvs.Add(uvA ?? new Vector2(0.5f, 0.0f));
            uvs.Add(uvB ?? new Vector2(0.5f, 0.0f));
            uvs.Add(uvC ?? new Vector2(0.5f, 0.0f));
            colors.Add(new Color(1f, 1f, 1f, alpha));
            colors.Add(new Color(1f, 1f, 1f, alpha));
            colors.Add(new Color(1f, 1f, 1f, alpha));
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
        }

        /// <summary>
        /// Constrói a geometria de um seixo/rocha facetada low-poly com normais planas e peso de vento zero.
        /// </summary>
        private static void BuildFacetedPebble(
            Vector3 center, float radius, float height, float irregularity,
            List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<Color> colors, List<int> tris)
        {
            const int sides = 6;
            Vector3[] basePoints = new Vector3[sides];
            Vector3[] midPoints = new Vector3[sides];
            Vector3 topPoint1 = center + new Vector3(-radius * 0.22f, height, 0f);
            Vector3 topPoint2 = center + new Vector3(radius * 0.22f, height * 0.90f, 0f);

            float angleStep = Mathf.PI * 2.0f / sides;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * angleStep;
                float rNoise = 1.0f + Mathf.Sin(angle * 2.5f) * irregularity;
                float bx = center.x + Mathf.Cos(angle) * radius * 0.82f * rNoise;
                float bz = center.z + Mathf.Sin(angle) * radius * 0.82f * rNoise;
                basePoints[i] = new Vector3(bx, center.y, bz);

                float mx = center.x + Mathf.Cos(angle) * radius * 1.08f * rNoise;
                float mz = center.z + Mathf.Sin(angle) * radius * 1.08f * rNoise;
                midPoints[i] = new Vector3(mx, center.y + height * 0.42f, mz);
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                // Faceta inferior
                AddFlatFace(basePoints[i], basePoints[next], midPoints[next], verts, normals, uvs, colors, tris, 0f);
                AddFlatFace(basePoints[i], midPoints[next], midPoints[i], verts, normals, uvs, colors, tris, 0f);

                // Faceta superior até a crista do topo
                Vector3 topTarget = (i < 3) ? topPoint1 : topPoint2;
                AddFlatFace(midPoints[i], midPoints[next], topTarget, verts, normals, uvs, colors, tris, 0f);
            }
            // Facetas de fechamento do topo
            AddFlatFace(midPoints[0], topPoint1, topPoint2, verts, normals, uvs, colors, tris, 0f);
            AddFlatFace(midPoints[3], topPoint2, topPoint1, verts, normals, uvs, colors, tris, 0f);
        }

        /// <summary>
        /// Gera uma malha de seixo/pedra pequena individual facetada low-poly (Little Pebble).
        /// Totalmente estática (Alpha=0, zero vento) e com normais facetadas próprias.
        /// </summary>
        public static Mesh CreateLittlePebbleMesh(float radius = 0.20f, float height = 0.14f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_LittlePebble_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            BuildFacetedPebble(Vector3.zero, radius, height, 0.18f, verts, normals, uvs, colors, tris);

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera um aglomerado orgânico de 3 a 5 pequenas pedras/seixos (Pebble Cluster).
        /// Ideal para quebrar a uniformidade da grama, adornar bases de escarpas e margens.
        /// </summary>
        public static Mesh CreatePebbleClusterMesh(int pebbleCount = 4, float radius = 0.45f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_PebbleCluster_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            // Pedra principal central
            BuildFacetedPebble(Vector3.zero, radius * 0.42f, radius * 0.30f, 0.20f, verts, normals, uvs, colors, tris);

            // Seixos menores distribuídos ao redor
            int satelliteCount = Mathf.Clamp(pebbleCount - 1, 2, 5);
            float angleStep = Mathf.PI * 2.0f / satelliteCount;

            for (int i = 0; i < satelliteCount; i++)
            {
                float ang = i * angleStep + Random.Range(-0.35f, 0.35f);
                float dist = radius * Random.Range(0.48f, 0.88f);
                Vector3 center = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                float pRad = radius * Random.Range(0.20f, 0.32f);
                float pH = pRad * Random.Range(0.65f, 0.85f);

                BuildFacetedPebble(center, pRad, pH, 0.22f, verts, normals, uvs, colors, tris);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma pedra de rio achatada e arredondada (Smooth River Stone).
        /// Perfeita para a zona de orla/margem de água (Water Spots) e leitos de lagos.
        /// </summary>
        public static Mesh CreateRiverStoneMesh(float radiusX = 0.32f, float radiusZ = 0.22f, float height = 0.09f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_RiverStone_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            const int sides = 8;
            Vector3[] basePoints = new Vector3[sides];
            Vector3[] midPoints = new Vector3[sides];
            Vector3 topCenter = new Vector3(0f, height, 0f);

            float angleStep = Mathf.PI * 2.0f / sides;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * angleStep;
                float rx = Mathf.Cos(angle) * radiusX;
                float rz = Mathf.Sin(angle) * radiusZ;

                basePoints[i] = new Vector3(rx * 0.85f, 0f, rz * 0.85f);
                midPoints[i] = new Vector3(rx * 1.05f, height * 0.45f, rz * 1.05f);
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                AddFlatFace(basePoints[i], basePoints[next], midPoints[next], verts, normals, uvs, colors, tris, 0f);
                AddFlatFace(basePoints[i], midPoints[next], midPoints[i], verts, normals, uvs, colors, tris, 0f);
                AddFlatFace(midPoints[i], midPoints[next], topCenter, verts, normals, uvs, colors, tris, 0f);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma rocha lascada/angular de cascalho e encostas (Scree / Talus Rock).
        /// Excelente para o sopé de montanhas rochosas e transições de escarpas.
        /// </summary>
        public static Mesh CreateScreeRockMesh(float size = 0.28f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_ScreeRock_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            // Vértices de uma cunha triangular angular
            Vector3 b0 = new Vector3(-size * 0.5f, 0f, -size * 0.4f);
            Vector3 b1 = new Vector3(size * 0.6f, 0f, -size * 0.3f);
            Vector3 b2 = new Vector3(0f, 0f, size * 0.6f);
            Vector3 peak = new Vector3(-size * 0.1f, size * 0.65f, -size * 0.1f);
            Vector3 fractureEdge = new Vector3(size * 0.25f, size * 0.38f, size * 0.15f);

            // Facetas triangulares angulares
            AddFlatFace(b0, b1, peak, verts, normals, uvs, colors, tris, 0f);
            AddFlatFace(b1, fractureEdge, peak, verts, normals, uvs, colors, tris, 0f);
            AddFlatFace(b1, b2, fractureEdge, verts, normals, uvs, colors, tris, 0f);
            AddFlatFace(b2, b0, peak, verts, normals, uvs, colors, tris, 0f);
            AddFlatFace(b2, peak, fractureEdge, verts, normals, uvs, colors, tris, 0f);

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        #endregion

        #region Botanical Weeds & Wild Undergrowth (Plantas Silvestres)

        /// <summary>
        /// Gera uma malha de tufo de ervas daninhas / dente-de-leão / cardo silvestre (Wild Weed Tuft).
        /// Apresenta roseta basal de folhas serrilhadas rentes ao solo e 1 a 2 hastes delgadas com botões.
        /// </summary>
        public static Mesh CreateWildWeedTuftMesh(int leafCount = 6, float radius = 0.48f, float height = 0.42f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_WildWeedTuft_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            float angleStep = 360f / leafCount;

            // 1. Roseta de folhas basais serrilhadas rentes ao solo
            for (int i = 0; i < leafCount; i++)
            {
                float baseAngle = i * angleStep + Random.Range(-10f, 10f);
                float rad = baseAngle * Mathf.Deg2Rad;
                Vector3 leafDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 leafSide = new Vector3(-leafDir.z, 0f, leafDir.x);

                float lLen = radius * Random.Range(0.85f, 1.15f);
                float lWidth = 0.12f * Random.Range(0.85f, 1.15f);
                float archH = 0.08f;

                int baseIdx = verts.Count;

                Vector3 v0 = Vector3.zero;
                Vector3 v1_L = leafDir * (lLen * 0.45f) - leafSide * (lWidth * 0.5f) + Vector3.up * archH;
                Vector3 v1_R = leafDir * (lLen * 0.45f) + leafSide * (lWidth * 0.5f) + Vector3.up * archH;
                Vector3 v2_tip = leafDir * lLen + Vector3.up * 0.02f;

                Vector3 norm = Vector3.up;

                verts.Add(v0);
                verts.Add(v1_L);
                verts.Add(v1_R);
                verts.Add(v2_tip);

                normals.Add(norm);
                normals.Add(norm);
                normals.Add(norm);
                normals.Add(norm);

                uvs.Add(new Vector2(0.5f, 0.0f));
                uvs.Add(new Vector2(0.1f, 0.45f));
                uvs.Add(new Vector2(0.9f, 0.45f));
                uvs.Add(new Vector2(0.5f, 0.90f));

                colors.Add(new Color(1f, 1f, 1f, 0.0f));  // Base imóvel
                colors.Add(new Color(1f, 1f, 1f, 0.28f));
                colors.Add(new Color(1f, 1f, 1f, 0.28f));
                colors.Add(new Color(1f, 1f, 1f, 0.40f)); // Ponta oscila sutilmente

                // Triângulos (dois lados)
                tris.Add(baseIdx + 0); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
            }

            // 2. Hastes delgadas com botão/flor silvestre
            int stalks = Random.Range(1, 3);
            for (int s = 0; s < stalks; s++)
            {
                float sAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector3 sOffset = new Vector3(Mathf.Cos(sAngle), 0f, Mathf.Sin(sAngle)) * (radius * 0.20f);
                float sHeight = height * Random.Range(0.85f, 1.15f);

                int sIdx = verts.Count;
                Vector3 s0_L = sOffset - Vector3.right * 0.025f;
                Vector3 s0_R = sOffset + Vector3.right * 0.025f;
                Vector3 s1_L = sOffset + Vector3.up * sHeight - Vector3.right * 0.020f;
                Vector3 s1_R = sOffset + Vector3.up * sHeight + Vector3.right * 0.020f;

                verts.Add(s0_L); verts.Add(s0_R); verts.Add(s1_L); verts.Add(s1_R);
                normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.4f, 0.0f)); uvs.Add(new Vector2(0.6f, 0.0f));
                uvs.Add(new Vector2(0.4f, 0.85f)); uvs.Add(new Vector2(0.6f, 0.85f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.85f));
                colors.Add(new Color(1f, 1f, 1f, 0.85f));

                tris.Add(sIdx + 0); tris.Add(sIdx + 2); tris.Add(sIdx + 1);
                tris.Add(sIdx + 1); tris.Add(sIdx + 2); tris.Add(sIdx + 3);
                tris.Add(sIdx + 0); tris.Add(sIdx + 1); tris.Add(sIdx + 2);
                tris.Add(sIdx + 1); tris.Add(sIdx + 3); tris.Add(sIdx + 2);

                // Botão floral / tufo de sementes (accent color UV.x >= 2.0f)
                int budIdx = verts.Count;
                float budRadius = 0.055f;
                Vector3 budCenter = sOffset + Vector3.up * (sHeight + 0.03f);

                Vector3 bL = budCenter - Vector3.right * budRadius;
                Vector3 bR = budCenter + Vector3.right * budRadius;
                Vector3 bT = budCenter + Vector3.up * (budRadius * 1.3f);
                Vector3 bB = budCenter - Vector3.up * (budRadius * 0.7f);

                verts.Add(bL); verts.Add(bR); verts.Add(bT); verts.Add(bB);
                normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up);
                // UV.x = 2.5f marca para cor de destaque (accent)
                uvs.Add(new Vector2(2.1f, 0.5f)); uvs.Add(new Vector2(2.9f, 0.5f));
                uvs.Add(new Vector2(2.5f, 1.0f)); uvs.Add(new Vector2(2.5f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.90f));
                colors.Add(new Color(1f, 1f, 1f, 0.90f));
                colors.Add(new Color(1f, 1f, 1f, 0.95f));
                colors.Add(new Color(1f, 1f, 1f, 0.85f));

                tris.Add(budIdx + 0); tris.Add(budIdx + 2); tris.Add(budIdx + 1);
                tris.Add(budIdx + 0); tris.Add(budIdx + 1); tris.Add(budIdx + 3);
                tris.Add(budIdx + 0); tris.Add(budIdx + 1); tris.Add(budIdx + 2);
                tris.Add(budIdx + 0); tris.Add(budIdx + 3); tris.Add(budIdx + 1);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de erva de folhas largas / tanchagem / folhagem rasteira de sombra (Broadleaf Weed).
        /// Folhas largas ovais que abraçam o relevo, excelentes sob copas de árvores e bordas úmidas.
        /// </summary>
        public static Mesh CreateBroadleafWeedMesh(int leafCount = 5, float radius = 0.42f, float height = 0.22f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_BroadleafWeed_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            float angleStep = 360f / leafCount;

            for (int i = 0; i < leafCount; i++)
            {
                float baseAngle = i * angleStep + Random.Range(-12f, 12f);
                float rad = baseAngle * Mathf.Deg2Rad;
                Vector3 leafDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 leafSide = new Vector3(-leafDir.z, 0f, leafDir.x);

                float lLen = radius * Random.Range(0.85f, 1.15f);
                float lWidth = 0.18f * Random.Range(0.85f, 1.15f);
                float midY = height * Random.Range(0.65f, 0.95f);

                int startIdx = verts.Count;

                // Base estreita
                Vector3 v0_L = -leafSide * (lWidth * 0.22f);
                Vector3 v0_R = leafSide * (lWidth * 0.22f);

                // Meio amplo (formato de remo/paddle)
                Vector3 v1_L = leafDir * (lLen * 0.50f) - leafSide * (lWidth * 0.52f) + Vector3.up * midY;
                Vector3 v1_R = leafDir * (lLen * 0.50f) + leafSide * (lWidth * 0.52f) + Vector3.up * midY;

                // Ponta arredondada caindo suavemente para o solo
                Vector3 v2_tip = leafDir * lLen + Vector3.up * 0.04f;

                Vector3 norm = (Vector3.up * 0.92f + leafDir * 0.08f).normalized;

                verts.Add(v0_L); verts.Add(v0_R); verts.Add(v1_L); verts.Add(v1_R); verts.Add(v2_tip);
                normals.Add(norm); normals.Add(norm); normals.Add(norm); normals.Add(norm); normals.Add(norm);

                uvs.Add(new Vector2(0.2f, 0.0f)); uvs.Add(new Vector2(0.8f, 0.0f));
                uvs.Add(new Vector2(0.05f, 0.55f)); uvs.Add(new Vector2(0.95f, 0.55f));
                uvs.Add(new Vector2(0.5f, 0.95f));

                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.25f));
                colors.Add(new Color(1f, 1f, 1f, 0.25f));
                colors.Add(new Color(1f, 1f, 1f, 0.40f));

                // Triângulos frente e verso
                tris.Add(startIdx + 0); tris.Add(startIdx + 2); tris.Add(startIdx + 1);
                tris.Add(startIdx + 1); tris.Add(startIdx + 2); tris.Add(startIdx + 3);
                tris.Add(startIdx + 2); tris.Add(startIdx + 4); tris.Add(startIdx + 3);

                tris.Add(startIdx + 0); tris.Add(startIdx + 1); tris.Add(startIdx + 2);
                tris.Add(startIdx + 1); tris.Add(startIdx + 3); tris.Add(startIdx + 2);
                tris.Add(startIdx + 2); tris.Add(startIdx + 3); tris.Add(startIdx + 4);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de hastes altas selvagens com plumas de sementes / trigo bravo (Tall Stalk Weed / Foxtail).
        /// Altura destacada (1.2m - 1.5m) que oscila vigorosamente com rajadas de vento.
        /// </summary>
        public static Mesh CreateTallStalkWeedMesh(int stalkCount = 4, float height = 1.35f, float baseWidth = 0.10f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_TallStalkWeed_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            for (int i = 0; i < stalkCount; i++)
            {
                float angle = (i * (360f / stalkCount) + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                Vector3 stalkDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 stalkSide = new Vector3(-stalkDir.z, 0f, stalkDir.x);

                float sHeight = height * Random.Range(0.85f, 1.20f);
                float lean = Random.Range(0.20f, 0.40f);
                Vector3 curveOffset = stalkDir * lean;

                int startIdx = verts.Count;

                // Base
                Vector3 v0_L = -stalkSide * (baseWidth * 0.5f);
                Vector3 v0_R = stalkSide * (baseWidth * 0.5f);

                // Meio da haste
                float midY = sHeight * 0.60f;
                Vector3 v1_L = -stalkSide * (baseWidth * 0.35f) + Vector3.up * midY + curveOffset * 0.40f;
                Vector3 v1_R = stalkSide * (baseWidth * 0.35f) + Vector3.up * midY + curveOffset * 0.40f;

                // Base da pluma / espiga de sementes
                float plumeBaseY = sHeight * 0.72f;
                Vector3 v2_L = -stalkSide * (baseWidth * 0.85f) + Vector3.up * plumeBaseY + curveOffset * 0.65f;
                Vector3 v2_R = stalkSide * (baseWidth * 0.85f) + Vector3.up * plumeBaseY + curveOffset * 0.65f;

                // Ponta da espiga
                Vector3 v3_tip = Vector3.up * sHeight + curveOffset;

                Vector3 norm = Vector3.up;

                verts.Add(v0_L); verts.Add(v0_R);
                verts.Add(v1_L); verts.Add(v1_R);
                verts.Add(v2_L); verts.Add(v2_R);
                verts.Add(v3_tip);

                normals.Add(norm); normals.Add(norm);
                normals.Add(norm); normals.Add(norm);
                normals.Add(norm); normals.Add(norm);
                normals.Add(norm);

                uvs.Add(new Vector2(0.2f, 0.0f)); uvs.Add(new Vector2(0.8f, 0.0f));
                uvs.Add(new Vector2(0.3f, 0.60f)); uvs.Add(new Vector2(0.7f, 0.60f));
                uvs.Add(new Vector2(0.1f, 0.75f)); uvs.Add(new Vector2(0.9f, 0.75f));
                uvs.Add(new Vector2(0.5f, 1.0f));

                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.45f));
                colors.Add(new Color(1f, 1f, 1f, 0.45f));
                colors.Add(new Color(1f, 1f, 1f, 0.85f));
                colors.Add(new Color(1f, 1f, 1f, 0.85f));
                colors.Add(new Color(1f, 1f, 1f, 1.0f)); // Pluma de topo com oscilação máxima

                // Triângulos (frente e verso)
                tris.Add(startIdx + 0); tris.Add(startIdx + 2); tris.Add(startIdx + 1);
                tris.Add(startIdx + 1); tris.Add(startIdx + 2); tris.Add(startIdx + 3);
                tris.Add(startIdx + 2); tris.Add(startIdx + 4); tris.Add(startIdx + 3);
                tris.Add(startIdx + 3); tris.Add(startIdx + 4); tris.Add(startIdx + 5);
                tris.Add(startIdx + 4); tris.Add(startIdx + 6); tris.Add(startIdx + 5);

                tris.Add(startIdx + 0); tris.Add(startIdx + 1); tris.Add(startIdx + 2);
                tris.Add(startIdx + 1); tris.Add(startIdx + 3); tris.Add(startIdx + 2);
                tris.Add(startIdx + 2); tris.Add(startIdx + 3); tris.Add(startIdx + 4);
                tris.Add(startIdx + 3); tris.Add(startIdx + 5); tris.Add(startIdx + 4);
                tris.Add(startIdx + 4); tris.Add(startIdx + 5); tris.Add(startIdx + 6);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de canteiro de trevos de 3 folhas (Clover Patch).
        /// Adiciona micro-detalhes charmosos que enriquecem o chão como em Zelda / Genshin.
        /// </summary>
        public static Mesh CreateCloverPatchMesh(int cloverCount = 7, float radius = 0.38f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_CloverPatch_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            for (int i = 0; i < cloverCount; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float dist = radius * Mathf.Sqrt(Random.Range(0.08f, 1.0f));
                Vector3 clovCenter = new Vector3(Mathf.Cos(ang) * dist, 0.05f, Mathf.Sin(ang) * dist);
                float clovSize = 0.055f * Random.Range(0.85f, 1.15f);

                // 3 folíolos em ângulos de 120 graus
                for (int f = 0; f < 3; f++)
                {
                    float leafAng = (f * 120f + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
                    Vector3 leafDir = new Vector3(Mathf.Cos(leafAng), 0f, Mathf.Sin(leafAng));
                    Vector3 leafSide = new Vector3(-leafDir.z, 0f, leafDir.x);

                    int startIdx = verts.Count;
                    Vector3 vBase = clovCenter;
                    Vector3 vMid_L = clovCenter + leafDir * (clovSize * 0.65f) - leafSide * (clovSize * 0.5f);
                    Vector3 vMid_R = clovCenter + leafDir * (clovSize * 0.65f) + leafSide * (clovSize * 0.5f);
                    Vector3 vTip = clovCenter + leafDir * clovSize + Vector3.up * 0.015f;

                    verts.Add(vBase); verts.Add(vMid_L); verts.Add(vMid_R); verts.Add(vTip);
                    normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up);
                    uvs.Add(new Vector2(0.5f, 0.0f)); uvs.Add(new Vector2(0.1f, 0.6f));
                    uvs.Add(new Vector2(0.9f, 0.6f)); uvs.Add(new Vector2(0.5f, 1.0f));
                    colors.Add(new Color(1f, 1f, 1f, 0.05f));
                    colors.Add(new Color(1f, 1f, 1f, 0.25f));
                    colors.Add(new Color(1f, 1f, 1f, 0.25f));
                    colors.Add(new Color(1f, 1f, 1f, 0.35f));

                    tris.Add(startIdx + 0); tris.Add(startIdx + 1); tris.Add(startIdx + 2);
                    tris.Add(startIdx + 1); tris.Add(startIdx + 3); tris.Add(startIdx + 2);
                    tris.Add(startIdx + 0); tris.Add(startIdx + 2); tris.Add(startIdx + 1);
                    tris.Add(startIdx + 1); tris.Add(startIdx + 2); tris.Add(startIdx + 3);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        #endregion

        #region Shoreline & Water Flora (Vegetação e Detalhes de Água)

        /// <summary>
        /// Gera uma malha densa de canteiro de taboas/juncos aquáticos (Water Cattail Bed).
        /// Combina lâminas aquáticas espessas com hastes portando a espiga cilíndrica marrom característica.
        /// </summary>
        public static Mesh CreateWaterCattailBedMesh(int reedCount = 10, int cattailCount = 3, float radius = 0.65f, float height = 1.55f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_WaterCattailBed_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            // 1. Lâminas aquáticas de fundo
            float angleStep = 360f / reedCount;
            for (int i = 0; i < reedCount; i++)
            {
                float baseAngle = i * angleStep + Random.Range(-12f, 12f);
                float rad = baseAngle * Mathf.Deg2Rad;
                Vector3 bladeDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 bladeSide = new Vector3(-bladeDir.z, 0f, bladeDir.x);

                float bDist = radius * Random.Range(0.2f, 0.85f);
                Vector3 root = bladeDir * bDist;
                float bHeight = height * Random.Range(0.70f, 0.95f);
                float lean = Random.Range(0.12f, 0.28f);
                Vector3 curve = bladeDir * lean;

                int baseIdx = verts.Count;
                float w = 0.12f;

                Vector3 v0_L = root - bladeSide * (w * 0.5f);
                Vector3 v0_R = root + bladeSide * (w * 0.5f);
                Vector3 v1_L = root - bladeSide * (w * 0.35f) + Vector3.up * (bHeight * 0.55f) + curve * 0.45f;
                Vector3 v1_R = root + bladeSide * (w * 0.35f) + Vector3.up * (bHeight * 0.55f) + curve * 0.45f;
                Vector3 v2_tip = root + Vector3.up * bHeight + curve;

                Vector3 norm = (Vector3.up * 0.90f + bladeDir * 0.10f).normalized;

                verts.Add(v0_L); verts.Add(v0_R); verts.Add(v1_L); verts.Add(v1_R); verts.Add(v2_tip);
                normals.Add(norm); normals.Add(norm); normals.Add(norm); normals.Add(norm); normals.Add(norm);

                uvs.Add(new Vector2(0.0f, 0.0f)); uvs.Add(new Vector2(1.0f, 0.0f));
                uvs.Add(new Vector2(0.15f, 0.55f)); uvs.Add(new Vector2(0.85f, 0.55f));
                uvs.Add(new Vector2(0.5f, 1.0f));

                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.45f));
                colors.Add(new Color(1f, 1f, 1f, 0.45f));
                colors.Add(new Color(1f, 1f, 1f, 0.85f));

                tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
                tris.Add(baseIdx + 2); tris.Add(baseIdx + 4); tris.Add(baseIdx + 3);

                tris.Add(baseIdx + 0); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 2); tris.Add(baseIdx + 3); tris.Add(baseIdx + 4);
            }

            // 2. Hastes de Taboa com espiga cilíndrica marrom característica
            for (int c = 0; c < cattailCount; c++)
            {
                float cAngle = (c * (360f / cattailCount) + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
                Vector3 cRoot = new Vector3(Mathf.Cos(cAngle), 0f, Mathf.Sin(cAngle)) * (radius * Random.Range(0.15f, 0.50f));
                float cHeight = height * Random.Range(0.90f, 1.15f);

                // Haste inferior
                int hIdx = verts.Count;
                float hw = 0.035f;
                float headBottom = cHeight * 0.70f;
                float headTop = cHeight * 0.90f;

                Vector3 h0_L = cRoot - Vector3.right * hw;
                Vector3 h0_R = cRoot + Vector3.right * hw;
                Vector3 h1_L = cRoot - Vector3.right * (hw * 0.8f) + Vector3.up * headBottom;
                Vector3 h1_R = cRoot + Vector3.right * (hw * 0.8f) + Vector3.up * headBottom;

                verts.Add(h0_L); verts.Add(h0_R); verts.Add(h1_L); verts.Add(h1_R);
                normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.4f, 0.0f)); uvs.Add(new Vector2(0.6f, 0.0f));
                uvs.Add(new Vector2(0.4f, 0.7f)); uvs.Add(new Vector2(0.6f, 0.7f));
                colors.Add(new Color(1f, 1f, 1f, 0.0f)); colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.65f)); colors.Add(new Color(1f, 1f, 1f, 0.65f));

                tris.Add(hIdx + 0); tris.Add(hIdx + 2); tris.Add(hIdx + 1);
                tris.Add(hIdx + 1); tris.Add(hIdx + 2); tris.Add(hIdx + 3);
                tris.Add(hIdx + 0); tris.Add(hIdx + 1); tris.Add(hIdx + 2);
                tris.Add(hIdx + 1); tris.Add(hIdx + 3); tris.Add(hIdx + 2);

                // Espiga cilíndrica de taboa (6 lados, UV.x = 2.5f para cor de destaque marrom aveludada)
                int cylIdx = verts.Count;
                const int cylSides = 6;
                float cylRad = 0.045f;
                float step = Mathf.PI * 2f / cylSides;

                for (int s = 0; s < cylSides; s++)
                {
                    float a0 = s * step;
                    float a1 = (s + 1) * step;

                    Vector3 pBot0 = cRoot + new Vector3(Mathf.Cos(a0) * cylRad, headBottom, Mathf.Sin(a0) * cylRad);
                    Vector3 pBot1 = cRoot + new Vector3(Mathf.Cos(a1) * cylRad, headBottom, Mathf.Sin(a1) * cylRad);
                    Vector3 pTop0 = cRoot + new Vector3(Mathf.Cos(a0) * cylRad, headTop, Mathf.Sin(a0) * cylRad);
                    Vector3 pTop1 = cRoot + new Vector3(Mathf.Cos(a1) * cylRad, headTop, Mathf.Sin(a1) * cylRad);

                    AddFlatFace(pBot0, pTop0, pTop1, verts, normals, uvs, colors, tris, 0.85f,
                        new Vector2(2.1f, 0.75f), new Vector2(2.1f, 0.90f), new Vector2(2.9f, 0.90f));
                    AddFlatFace(pBot0, pTop1, pBot1, verts, normals, uvs, colors, tris, 0.85f,
                        new Vector2(2.1f, 0.75f), new Vector2(2.9f, 0.90f), new Vector2(2.9f, 0.75f));
                }

                // Espícula fina no topo da taboa
                int spikeIdx = verts.Count;
                Vector3 spk0 = cRoot + Vector3.up * headTop;
                Vector3 spk1 = cRoot + Vector3.up * (cHeight + 0.12f);
                Vector3 spkL = spk0 - Vector3.right * 0.015f;
                Vector3 spkR = spk0 + Vector3.right * 0.015f;

                verts.Add(spkL); verts.Add(spkR); verts.Add(spk1);
                normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.4f, 0.90f)); uvs.Add(new Vector2(0.6f, 0.90f)); uvs.Add(new Vector2(0.5f, 1.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.85f)); colors.Add(new Color(1f, 1f, 1f, 0.85f)); colors.Add(new Color(1f, 1f, 1f, 1.0f));

                tris.Add(spikeIdx + 0); tris.Add(spikeIdx + 2); tris.Add(spikeIdx + 1);
                tris.Add(spikeIdx + 0); tris.Add(spikeIdx + 1); tris.Add(spikeIdx + 2);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        #endregion
    }
}
