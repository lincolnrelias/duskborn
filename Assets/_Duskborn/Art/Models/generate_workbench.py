import math
import os

class ObjBuilder:
    def __init__(self):
        self.vertices = []
        self.normals = []
        self.uvs = []
        self.groups = {}  # group_name: [faces]
        self.current_group = "Wood"

    def set_group(self, name):
        self.current_group = name
        if name not in self.groups:
            self.groups[name] = []

    def add_vertex(self, x, y, z):
        self.vertices.append((round(x, 5), round(y, 5), round(z, 5)))
        return len(self.vertices)

    def add_normal(self, nx, ny, nz):
        l = math.sqrt(nx*nx + ny*ny + nz*nz) or 1.0
        self.normals.append((round(nx/l, 5), round(ny/l, 5), round(nz/l, 5)))
        return len(self.normals)

    def add_uv(self, u, v):
        self.uvs.append((round(u, 5), round(v, 5)))
        return len(self.uvs)

    def add_box(self, min_x, max_x, min_y, max_y, min_z, max_z, group=None):
        if group:
            self.set_group(group)
        min_x, max_x = sorted([min_x, max_x])
        min_y, max_y = sorted([min_y, max_y])
        min_z, max_z = sorted([min_z, max_z])

        # 8 vertices
        v0 = self.add_vertex(min_x, min_y, min_z)
        v1 = self.add_vertex(max_x, min_y, min_z)
        v2 = self.add_vertex(max_x, max_y, min_z)
        v3 = self.add_vertex(min_x, max_y, min_z)
        v4 = self.add_vertex(min_x, min_y, max_z)
        v5 = self.add_vertex(max_x, min_y, max_z)
        v6 = self.add_vertex(max_x, max_y, max_z)
        v7 = self.add_vertex(min_x, max_y, max_z)

        # Normals
        n_front  = self.add_normal(0, 0, -1)
        n_back   = self.add_normal(0, 0, 1)
        n_left   = self.add_normal(-1, 0, 0)
        n_right  = self.add_normal(1, 0, 0)
        n_bottom = self.add_normal(0, -1, 0)
        n_top    = self.add_normal(0, 1, 0)

        # UVs
        u0 = self.add_uv(0, 0)
        u1 = self.add_uv(1, 0)
        u2 = self.add_uv(1, 1)
        u3 = self.add_uv(0, 1)
        quad_uv = [u0, u1, u2, u3]

        # Faces (CCW for outward viewing)
        # Front (-Z)
        self.add_quad(v0, v3, v2, v1, n_front, [u0, u3, u2, u1])
        # Back (+Z)
        self.add_quad(v5, v6, v7, v4, n_back, [u0, u3, u2, u1])
        # Left (-X)
        self.add_quad(v4, v7, v3, v0, n_left, [u0, u3, u2, u1])
        # Right (+X)
        self.add_quad(v1, v2, v6, v5, n_right, [u0, u3, u2, u1])
        # Bottom (-Y)
        self.add_quad(v4, v0, v1, v5, n_bottom, [u0, u3, u2, u1])
        # Top (+Y)
        self.add_quad(v3, v7, v6, v2, n_top, [u0, u3, u2, u1])

    def add_quad(self, v1, v2, v3, v4, normal_idx=None, uvs=None):
        u = uvs if uvs else [1, 1, 1, 1]
        if normal_idx is None:
            normal_idx = self._compute_face_normal(v1, v2, v3)
        self.groups[self.current_group].append(((v1, u[0], normal_idx), (v2, u[1], normal_idx), (v3, u[2], normal_idx)))
        self.groups[self.current_group].append(((v1, u[0], normal_idx), (v3, u[2], normal_idx), (v4, u[3], normal_idx)))

    def add_tri(self, v1, v2, v3, normal_idx=None, uvs=None):
        u = uvs if uvs else [1, 1, 1]
        if normal_idx is None:
            normal_idx = self._compute_face_normal(v1, v2, v3)
        self.groups[self.current_group].append(((v1, u[0], normal_idx), (v2, u[1], normal_idx), (v3, u[2], normal_idx)))

    def _compute_face_normal(self, v1, v2, v3):
        p1 = self.vertices[v1 - 1]
        p2 = self.vertices[v2 - 1]
        p3 = self.vertices[v3 - 1]
        ux, uy, uz = p2[0] - p1[0], p2[1] - p1[1], p2[2] - p1[2]
        vx, vy, vz = p3[0] - p1[0], p3[1] - p1[1], p3[2] - p1[2]
        nx = uy * vz - uz * vy
        ny = uz * vx - ux * vz
        nz = ux * vy - uy * vx
        return self.add_normal(nx, ny, nz)

    def add_cylinder(self, x_center, z_center, y_min, y_max, radius, segments=8, group=None):
        if group:
            self.set_group(group)
        pts_bot = []
        pts_top = []
        for i in range(segments):
            angle = 2.0 * math.pi * i / segments
            x = x_center + radius * math.cos(angle)
            z = z_center + radius * math.sin(angle)
            pts_bot.append(self.add_vertex(x, y_min, z))
            pts_top.append(self.add_vertex(x, y_max, z))

        u0 = self.add_uv(0, 0)
        u1 = self.add_uv(1, 0)
        u2 = self.add_uv(1, 1)
        u3 = self.add_uv(0, 1)
        quad_uv = [u0, u1, u2, u3]
        tri_uv = [u0, u1, u2]

        # Side quads
        for i in range(segments):
            ni = (i + 1) % segments
            self.add_quad(pts_bot[i], pts_top[i], pts_top[ni], pts_bot[ni], None, quad_uv)

        # Bottom cap (-Y)
        bot_center = self.add_vertex(x_center, y_min, z_center)
        n_bot = self.add_normal(0, -1, 0)
        for i in range(segments):
            ni = (i + 1) % segments
            self.add_tri(bot_center, pts_bot[ni], pts_bot[i], n_bot, tri_uv)

        # Top cap (+Y)
        top_center = self.add_vertex(x_center, y_max, z_center)
        n_top = self.add_normal(0, 1, 0)
        for i in range(segments):
            ni = (i + 1) % segments
            self.add_tri(top_center, pts_top[i], pts_top[ni], n_top, tri_uv)

    def add_cylinder_x(self, x_min, x_max, y_center, z_center, radius, segments=8, group=None):
        """Cylinder aligned along X axis (e.g. for grindstone wheel or axles)."""
        if group:
            self.set_group(group)
        pts_left = []
        pts_right = []
        for i in range(segments):
            angle = 2.0 * math.pi * i / segments
            y = y_center + radius * math.cos(angle)
            z = z_center + radius * math.sin(angle)
            pts_left.append(self.add_vertex(x_min, y, z))
            pts_right.append(self.add_vertex(x_max, y, z))

        u0 = self.add_uv(0, 0)
        u1 = self.add_uv(1, 0)
        u2 = self.add_uv(1, 1)
        u3 = self.add_uv(0, 1)
        quad_uv = [u0, u1, u2, u3]
        tri_uv = [u0, u1, u2]

        for i in range(segments):
            ni = (i + 1) % segments
            self.add_quad(pts_left[i], pts_left[ni], pts_right[ni], pts_right[i], None, quad_uv)

        # Left cap (-X)
        left_center = self.add_vertex(x_min, y_center, z_center)
        n_left = self.add_normal(-1, 0, 0)
        for i in range(segments):
            ni = (i + 1) % segments
            self.add_tri(left_center, pts_left[ni], pts_left[i], n_left, tri_uv)

        # Right cap (+X)
        right_center = self.add_vertex(x_max, y_center, z_center)
        n_right = self.add_normal(1, 0, 0)
        for i in range(segments):
            ni = (i + 1) % segments
            self.add_tri(right_center, pts_right[i], pts_right[ni], n_right, tri_uv)

    def export_obj(self, filepath, mtl_filename="Workbench.mtl"):
        with open(filepath, "w", encoding="utf-8") as f:
            f.write("# Duskborn Stylized Workbench Model\n")
            f.write(f"mtllib {mtl_filename}\n")
            f.write("o Workbench\n\n")

            for v in self.vertices:
                f.write(f"v {v[0]:.5f} {v[1]:.5f} {v[2]:.5f}\n")
            f.write("\n")

            for vt in self.uvs:
                f.write(f"vt {vt[0]:.5f} {vt[1]:.5f}\n")
            f.write("\n")

            for vn in self.normals:
                f.write(f"vn {vn[0]:.5f} {vn[1]:.5f} {vn[2]:.5f}\n")
            f.write("\n")

            for group_name in ["Wood", "Iron", "Stone"]:
                faces = self.groups.get(group_name, [])
                if not faces:
                    continue
                f.write(f"g {group_name}\n")
                f.write(f"usemtl {group_name}\n")
                f.write("s 1\n")
                for tri in faces:
                    f.write(f"f {tri[0][0]}/{tri[0][1]}/{tri[0][2]} {tri[1][0]}/{tri[1][1]}/{tri[1][2]} {tri[2][0]}/{tri[2][1]}/{tri[2][2]}\n")
                f.write("\n")


def build_workbench():
    b = ObjBuilder()

    # =========================================================================
    # 1. WOODEN STRUCTURE (WOOD)
    # =========================================================================
    b.set_group("Wood")

    # A) 4 Robust Heavy Corner Legs (tapered slightly or beveled)
    leg_w = 0.12
    leg_h = 0.74
    legs = [
        (-0.76, -0.64, -0.38, -0.26),  # Front-Left
        ( 0.64,  0.76, -0.38, -0.26),  # Front-Right
        (-0.76, -0.64,  0.26,  0.38),  # Back-Left
        ( 0.64,  0.76,  0.26,  0.38),  # Back-Right
    ]
    for lx0, lx1, lz0, lz1 in legs:
        b.add_box(lx0, lx1, 0.0, leg_h, lz0, lz1, group="Wood")

    # B) Upper Frame Rails (Apron under tabletop, Y in [0.62, 0.74])
    b.add_box(-0.64, 0.64, 0.62, 0.74, -0.36, -0.28, group="Wood")
    b.add_box(-0.64, 0.64, 0.62, 0.74,  0.28,  0.36, group="Wood")
    b.add_box(-0.74, -0.66, 0.62, 0.74, -0.26, 0.26, group="Wood")
    b.add_box( 0.66,  0.74, 0.62, 0.74, -0.26, 0.26, group="Wood")

    # C) Corner Gusset Braces under table corners
    b.add_box(-0.64, -0.46, 0.60, 0.66, -0.33, -0.31, group="Wood")
    b.add_box( 0.46,  0.64, 0.60, 0.66, -0.33, -0.31, group="Wood")
    b.add_box(-0.64, -0.46, 0.60, 0.66,  0.31,  0.33, group="Wood")
    b.add_box( 0.46,  0.64, 0.60, 0.66,  0.31,  0.33, group="Wood")

    # D) Lower Stretchers (Connecting legs at Y in [0.14, 0.20])
    b.add_box(-0.64, 0.64, 0.14, 0.20, -0.35, -0.29, group="Wood")
    b.add_box(-0.64, 0.64, 0.14, 0.20,  0.29,  0.35, group="Wood")
    b.add_box(-0.73, -0.67, 0.14, 0.20, -0.26, 0.26, group="Wood")
    b.add_box( 0.67,  0.73, 0.14, 0.20, -0.26, 0.26, group="Wood")

    # E) Lower Storage Shelf Slats (Resting on lower stretchers, Y in [0.20, 0.23])
    slat_count = 7
    slat_w = 0.12
    slat_spacing = 0.18
    for i in range(slat_count):
        sx = -0.54 + i * slat_spacing
        b.add_box(sx - slat_w*0.5, sx + slat_w*0.5, 0.20, 0.23, -0.30, 0.30, group="Wood")

    # F) Tabletop Planks (Heavy 3-plank timber surface, Y in [0.74, 0.83])
    planks = [
        (-0.43, -0.15),  # Front plank
        (-0.13,  0.13),  # Center plank
        ( 0.15,  0.43),  # Back plank
    ]
    for pz0, pz1 in planks:
        b.add_box(-0.85, 0.85, 0.74, 0.83, pz0, pz1, group="Wood")

    # G) Rear Tool Rack Board / Backboard (Y in [0.83, 1.15], Z in [0.38, 0.43])
    b.add_box(-0.35, -0.29, 0.83, 1.16, 0.38, 0.43, group="Wood")
    b.add_box( 0.45,  0.51, 0.83, 1.16, 0.38, 0.43, group="Wood")
    b.add_box(-0.33, 0.49, 1.00, 1.13, 0.39, 0.42, group="Wood")
    # Wooden tool pegs protruding from backboard
    for px in [-0.20, -0.05, 0.10, 0.25, 0.40]:
        b.add_box(px - 0.015, px + 0.015, 1.05, 1.08, 0.34, 0.39, group="Wood")

    # H) Carpentry Vise Wooden Outer Jaw (Front-Right corner)
    b.add_box(0.34, 0.66, 0.70, 0.83, -0.50, -0.44, group="Wood")
    # Vise wooden handle T-bar
    b.add_cylinder(0.50, -0.62, 0.67, 0.85, 0.016, segments=6, group="Wood")

    # I) Small Crafted Props on Table / Shelf (Group: Wood)
    # 1. Hammer Wooden Handle
    b.add_box(-0.05, 0.20, 0.83, 0.86, -0.12, -0.08, group="Wood")
    # 2. Hand Saw Wooden Grip Handle
    b.add_box(0.18, 0.26, 0.96, 1.08, 0.35, 0.38, group="Wood")
    # 3. Stacked Split Firewood / Wooden Billets on lower shelf
    b.add_cylinder_x(-0.48, -0.18, 0.27, -0.12, 0.045, segments=6, group="Wood")
    b.add_cylinder_x(-0.46, -0.20, 0.27,  0.00, 0.045, segments=6, group="Wood")
    b.add_cylinder_x(-0.44, -0.16, 0.33, -0.06, 0.040, segments=6, group="Wood")

    # =========================================================================
    # 2. IRON DETAILS & CRAFTING TOOLS (IRON)
    # =========================================================================
    b.set_group("Iron")

    # A) Blacksmith Anvil (Left side of workbench, X ~ -0.48, Z ~ 0.0)
    # 1. Flared Base Plate
    b.add_box(-0.64, -0.32, 0.83, 0.87, -0.19, 0.19, group="Iron")
    # 2. Narrower Anvil Waist
    b.add_box(-0.58, -0.38, 0.87, 0.93, -0.13, 0.13, group="Iron")
    # 3. Anvil Body & Main Striking Surface (Flat face)
    b.add_box(-0.55, -0.30, 0.93, 1.02, -0.15, 0.15, group="Iron")
    # 4. Tapered Anvil Horn (Pointing outward towards -X)
    b.add_box(-0.64, -0.55, 0.94, 1.01, -0.12, 0.12, group="Iron")
    b.add_box(-0.71, -0.64, 0.95, 1.00, -0.08, 0.08, group="Iron")
    b.add_box(-0.76, -0.71, 0.96, 0.99, -0.04, 0.04, group="Iron")
    # 5. Anvil Heel & Step (Towards +X)
    b.add_box(-0.30, -0.23, 0.93, 0.98, -0.13, 0.13, group="Iron")
    # 6. Hardy Hole (Indented dark cavity on heel)
    b.add_box(-0.28, -0.25, 0.98, 1.00, -0.03, 0.03, group="Iron")

    # B) Tabletop Corner Brackets & Reinforcement Straps (Stylized Muck/Duskborn)
    cw = 0.09
    ct = 0.012
    b.add_box(-0.855, -0.855 + cw, 0.735, 0.835, -0.435, -0.435 + ct, group="Iron")
    b.add_box(-0.855, -0.855 + ct, 0.735, 0.835, -0.435, -0.435 + cw, group="Iron")
    b.add_box( 0.855 - cw,  0.855, 0.735, 0.835, -0.435, -0.435 + ct, group="Iron")
    b.add_box( 0.855 - ct,  0.855, 0.735, 0.835, -0.435, -0.435 + cw, group="Iron")
    b.add_box(-0.855, -0.855 + cw, 0.735, 0.835,  0.435 - ct,  0.435, group="Iron")
    b.add_box(-0.855, -0.855 + ct, 0.735, 0.835,  0.435 - cw,  0.435, group="Iron")
    b.add_box( 0.855 - cw,  0.855, 0.735, 0.835,  0.435 - ct,  0.435, group="Iron")
    b.add_box( 0.855 - ct,  0.855, 0.735, 0.835,  0.435 - cw,  0.435, group="Iron")

    # C) Vise Iron Mechanism
    b.add_cylinder_x(0.48, 0.52, 0.765, -0.56, 0.024, segments=6, group="Iron")
    b.add_box(0.47, 0.53, 0.74, 0.79, -0.63, -0.57, group="Iron")
    b.add_box(0.38, 0.41, 0.75, 0.78, -0.53, -0.44, group="Iron")
    b.add_box(0.59, 0.62, 0.75, 0.78, -0.53, -0.44, group="Iron")

    # D) Tools Resting on Bench
    # 1. Mallet/Hammer Head on table (sitting on handle)
    b.add_box(-0.11, -0.04, 0.83, 0.90, -0.15, -0.05, group="Iron")
    # 2. Hand Saw Blade (hanging from rack)
    b.add_box(0.04, 0.20, 0.99, 1.05, 0.36, 0.375, group="Iron")
    # 3. Iron Forged Ingots / Bars stacked on lower shelf
    b.add_box(0.08, 0.28, 0.23, 0.28, -0.14, -0.02, group="Iron")
    b.add_box(0.08, 0.28, 0.23, 0.28,  0.03,  0.15, group="Iron")
    b.add_box(0.12, 0.24, 0.28, 0.33, -0.10,  0.10, group="Iron")

    # E) Grindstone Iron Mount & Axle
    b.add_box(0.68, 0.74, 0.83, 0.98, 0.08, 0.12, group="Iron")
    b.add_box(0.68, 0.74, 0.83, 0.98, 0.24, 0.28, group="Iron")
    b.add_box(0.69, 0.83, 0.96, 1.00, 0.16, 0.20, group="Iron")
    b.add_box(0.82, 0.84, 0.96, 1.09, 0.17, 0.19, group="Iron")
    b.add_box(0.83, 0.88, 1.07, 1.09, 0.17, 0.19, group="Iron")

    # F) Iron Rivets / Studs on Bench Leg Collars
    rivet_s = 0.012
    for lx0, lx1, lz0, lz1 in legs:
        cx = (lx0 + lx1) * 0.5
        cz = (lz0 + lz1) * 0.5
        sgn_z = -1 if cz < 0 else 1
        z_r = (lz0 - rivet_s) if sgn_z < 0 else lz1
        b.add_box(cx - rivet_s, cx + rivet_s, 0.68, 0.71, z_r, z_r + rivet_s, group="Iron")

    # =========================================================================
    # 3. STONE ELEMENTS (STONE)
    # =========================================================================
    b.set_group("Stone")

    # A) Sharpening Grindstone Wheel (Mounted on right side of bench)
    b.add_cylinder_x(0.73, 0.80, 0.98, 0.18, 0.15, segments=10, group="Stone")

    # B) Flat Rectangular Whetstone (Sharpening stone slab lying on tabletop)
    b.add_box(-0.22, -0.06, 0.83, 0.86, 0.12, 0.26, group="Stone")

    # C) Rough Hewn Stone Block / Masonry Chisel Block on Lower Shelf
    b.add_box(0.36, 0.54, 0.23, 0.36, -0.08, 0.12, group="Stone")

    return b


if __name__ == "__main__":
    builder = build_workbench()
    script_dir = os.path.dirname(os.path.abspath(__file__))
    obj_path = os.path.join(script_dir, "Workbench.obj")
    mtl_path = os.path.join(script_dir, "Workbench.mtl")

    builder.export_obj(obj_path, "Workbench.mtl")

    with open(mtl_path, "w", encoding="utf-8") as f:
        f.write("# Duskborn Workbench Material Library\n")
        f.write("newmtl Wood\nKd 0.63 0.48 0.35\n\n")
        f.write("newmtl Iron\nKd 0.22 0.24 0.27\n\n")
        f.write("newmtl Stone\nKd 0.55 0.55 0.55\n\n")

    total_tris = sum(len(faces) for faces in builder.groups.values())
    print(f"Generated {obj_path} with {len(builder.vertices)} vertices and {total_tris} triangles.")
    for grp, faces in builder.groups.items():
        print(f" - {grp}: {len(faces)} tris")
