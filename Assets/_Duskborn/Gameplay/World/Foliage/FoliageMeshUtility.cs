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
    }
}
