import math

class ObjBuilder:
    def __init__(self):
        self.vertices = []
        self.normals = []
        self.uvs = []
        self.groups = {} # group_name: [faces]
        self.current_group = "default"

    def set_group(self, name):
        self.current_group = name
        if name not in self.groups:
            self.groups[name] = []

    def add_vertex(self, x, y, z):
        self.vertices.append((x, y, z))
        return len(self.vertices)

    def add_normal(self, nx, ny, nz):
        self.normals.append((nx, ny, nz))
        return len(self.normals)

    def add_uv(self, u, v):
        self.uvs.append((u, v))
        return len(self.uvs)

    def add_box(self, min_x, max_x, min_y, max_y, min_z, max_z, group=None):
        if group:
            self.set_group(group)
        min_x, max_x = sorted([min_x, max_x])
        min_y, max_y = sorted([min_y, max_y])
        min_z, max_z = sorted([min_z, max_z])
        # 8 vertices
        # 0: ---, 1: +--, 2: ++-, 3: -+-, 4: --+, 5: +-+, 6: +++, 7: -++
        v = [
            self.add_vertex(min_x, min_y, min_z),
            self.add_vertex(max_x, min_y, min_z),
            self.add_vertex(max_x, max_y, min_z),
            self.add_vertex(min_x, max_y, min_z),
            self.add_vertex(min_x, min_y, max_z),
            self.add_vertex(max_x, min_y, max_z),
            self.add_vertex(max_x, max_y, max_z),
            self.add_vertex(min_x, max_y, max_z),
        ]
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

        # Faces (CCW winding for outside view)
        # Front (-Z)
        self.add_quad(v[0], v[3], v[2], v[1], n_front, [u0, u3, u2, u1])
        # Back (+Z)
        self.add_quad(v[5], v[6], v[7], v[4], n_back, [u0, u3, u2, u1])
        # Left (-X)
        self.add_quad(v[4], v[7], v[3], v[0], n_left, [u0, u3, u2, u1])
        # Right (+X)
        self.add_quad(v[1], v[2], v[6], v[5], n_right, [u0, u3, u2, u1])
        # Bottom (-Y)
        self.add_quad(v[4], v[0], v[1], v[5], n_bottom, [u0, u3, u2, u1])
        # Top (+Y)
        self.add_quad(v[3], v[7], v[6], v[2], n_top, [u0, u3, u2, u1])

    def add_quad(self, v1, v2, v3, v4, normal_idx, uvs=None):
        u = uvs if uvs else [1, 1, 1, 1]
        # Quad split into 2 tris: (v1, v2, v3) and (v1, v3, v4)
        self.groups[self.current_group].append(((v1, u[0], normal_idx), (v2, u[1], normal_idx), (v3, u[2], normal_idx)))
        self.groups[self.current_group].append(((v1, u[0], normal_idx), (v3, u[2], normal_idx), (v4, u[3], normal_idx)))

    def add_tri(self, v1, v2, v3, normal_idx, uvs=None):
        u = uvs if uvs else [1, 1, 1]
        self.groups[self.current_group].append(((v1, u[0], normal_idx), (v2, u[1], normal_idx), (v3, u[2], normal_idx)))

    def export_obj(self, filepath, mtl_filename="Chest.mtl"):
        with open(filepath, "w", encoding="utf-8") as f:
            f.write("# Duskborn Stylized Chest Model\n")
            f.write(f"mtllib {mtl_filename}\n")
            f.write("o Chest\n\n")

            for v in self.vertices:
                f.write(f"v {v[0]:.5f} {v[1]:.5f} {v[2]:.5f}\n")
            f.write("\n")

            for vt in self.uvs:
                f.write(f"vt {vt[0]:.5f} {vt[1]:.5f}\n")
            f.write("\n")

            for vn in self.normals:
                f.write(f"vn {vn[0]:.5f} {vn[1]:.5f} {vn[2]:.5f}\n")
            f.write("\n")

            for group_name, faces in self.groups.items():
                if not faces:
                    continue
                f.write(f"g {group_name}\n")
                f.write(f"usemtl {group_name}\n")
                f.write("s 1\n")
                for tri in faces:
                    f.write(f"f {tri[0][0]}/{tri[0][1]}/{tri[0][2]} {tri[1][0]}/{tri[1][1]}/{tri[1][2]} {tri[2][0]}/{tri[2][1]}/{tri[2][2]}\n")
                f.write("\n")


def build_chest():
    b = ObjBuilder()

    # 1. BASE BODY (WOOD)
    # Dimensions: X [-0.40, 0.40], Y [0.0, 0.38], Z [-0.26, 0.26]
    b.add_box(-0.40, 0.40, 0.0, 0.38, -0.26, 0.26, group="Wood")

    # 2. LID (WOOD) - Barrel Vault
    # 8 segments from -90 deg (front, Z=-0.27, Y=0.38) to +90 deg (back, Z=0.27, Y=0.38)
    b.set_group("Wood")
    segs = 8
    radius = 0.27
    cy = 0.38
    lid_min_x = -0.41
    lid_max_x = 0.41

    arc_pts = []
    for i in range(segs + 1):
        angle = -math.pi / 2.0 + (math.pi * i / segs)
        z = radius * math.sin(angle)
        y = cy + radius * math.cos(angle)
        arc_pts.append((y, z))

    # Add vertices along arc for left (min_x) and right (max_x)
    left_arc_v = [b.add_vertex(lid_min_x, y, z) for y, z in arc_pts]
    right_arc_v = [b.add_vertex(lid_max_x, y, z) for y, z in arc_pts]

    # Curved outer faces
    uv0 = b.add_uv(0, 0)
    uv1 = b.add_uv(1, 0)
    uv2 = b.add_uv(1, 1)
    uv3 = b.add_uv(0, 1)

    for i in range(segs):
        y1, z1 = arc_pts[i]
        y2, z2 = arc_pts[i+1]
        mid_y = (y1 + y2) * 0.5 - cy
        mid_z = (z1 + z2) * 0.5
        norm_len = math.sqrt(mid_y*mid_y + mid_z*mid_z) or 1.0
        n = b.add_normal(0, mid_y / norm_len, mid_z / norm_len)
        # quad: left[i], left[i+1], right[i+1], right[i] (outward CCW/Unity CW facing)
        b.add_quad(left_arc_v[i], left_arc_v[i+1], right_arc_v[i+1], right_arc_v[i], n, [uv0, uv3, uv2, uv1])

    # End caps (left at -X, right at +X)
    n_left = b.add_normal(-1, 0, 0)
    n_right = b.add_normal(1, 0, 0)
    # Fan triangulate from center (lid_min_x, cy, 0)
    v_left_center = b.add_vertex(lid_min_x, cy, 0)
    v_right_center = b.add_vertex(lid_max_x, cy, 0)
    for i in range(segs):
        # Left end cap (-X) - CCW viewed from -X
        b.add_tri(v_left_center, left_arc_v[i+1], left_arc_v[i], n_left, [uv0, uv1, uv2])
        # Right end cap (+X) - CCW viewed from +X
        b.add_tri(v_right_center, right_arc_v[i], right_arc_v[i+1], n_right, [uv0, uv1, uv2])

    # 3. IRON DETAILS (IRON)
    # a) Bottom skirt base trim
    b.add_box(-0.415, 0.415, 0.0, 0.05, -0.275, 0.275, group="Iron")
    # b) Top rim band on base
    b.add_box(-0.415, 0.415, 0.34, 0.38, -0.275, 0.275, group="Iron")

    # c) 4 Corner brackets on base
    bw = 0.055
    bt = 0.012
    # Front-Left corner
    b.add_box(-0.415, -0.415 + bw, 0.0, 0.38, -0.275, -0.275 + bt, group="Iron")
    b.add_box(-0.415, -0.415 + bt, 0.0, 0.38, -0.275, -0.275 + bw, group="Iron")
    # Front-Right corner
    b.add_box(0.415 - bw, 0.415, 0.0, 0.38, -0.275, -0.275 + bt, group="Iron")
    b.add_box(0.415 - bt, 0.415, 0.0, 0.38, -0.275, -0.275 + bw, group="Iron")
    # Back-Left corner
    b.add_box(-0.415, -0.415 + bw, 0.0, 0.38, 0.275 - bt, 0.275, group="Iron")
    b.add_box(-0.415, -0.415 + bt, 0.0, 0.38, 0.275 - bw, 0.275, group="Iron")
    # Back-Right corner
    b.add_box(0.415 - bw, 0.415, 0.0, 0.38, 0.275 - bt, 0.275, group="Iron")
    b.add_box(0.415 - bt, 0.415, 0.0, 0.38, 0.275 - bw, 0.275, group="Iron")

    # d) Base vertical straps (Band 1 at -0.19, Band 2 at +0.19, width 0.055)
    sw = 0.027
    for sx in [-0.19, 0.19]:
        # Front strap
        b.add_box(sx - sw, sx + sw, 0.0, 0.38, -0.275, -0.26, group="Iron")
        # Back strap
        b.add_box(sx - sw, sx + sw, 0.0, 0.38, 0.26, 0.275, group="Iron")

    # e) Lid Rim Band (along bottom seam of lid)
    b.add_box(-0.42, 0.42, 0.38, 0.41, -0.285, 0.285, group="Iron")

    # f) Lid arched straps (matching base straps)
    lid_strap_r = 0.285
    for sx in [-0.19, 0.19]:
        s_left = sx - sw
        s_right = sx + sw
        strap_pts = []
        for i in range(segs + 1):
            angle = -math.pi / 2.0 + (math.pi * i / segs)
            z = lid_strap_r * math.sin(angle)
            y = cy + lid_strap_r * math.cos(angle)
            strap_pts.append((y, z))
        s_lv = [b.add_vertex(s_left, y, z) for y, z in strap_pts]
        s_rv = [b.add_vertex(s_right, y, z) for y, z in strap_pts]
        for i in range(segs):
            y1, z1 = strap_pts[i]
            y2, z2 = strap_pts[i+1]
            my = (y1 + y2) * 0.5 - cy
            mz = (z1 + z2) * 0.5
            nl = math.sqrt(my*my + mz*mz) or 1.0
            n = b.add_normal(0, my / nl, mz / nl)
            b.add_quad(s_lv[i], s_lv[i+1], s_rv[i+1], s_rv[i], n, [uv0, uv3, uv2, uv1])

    # g) Lid Arched Side Trim (Left & Right ends)
    trim_w = 0.025
    trim_r = 0.282
    for sx, is_left in [(-0.42, True), (0.42 - trim_w, False)]:
        s_left = sx
        s_right = sx + trim_w
        t_pts = []
        for i in range(segs + 1):
            angle = -math.pi / 2.0 + (math.pi * i / segs)
            z = trim_r * math.sin(angle)
            y = cy + trim_r * math.cos(angle)
            t_pts.append((y, z))
        s_lv = [b.add_vertex(s_left, y, z) for y, z in t_pts]
        s_rv = [b.add_vertex(s_right, y, z) for y, z in t_pts]
        for i in range(segs):
            y1, z1 = t_pts[i]
            y2, z2 = t_pts[i+1]
            my = (y1 + y2) * 0.5 - cy
            mz = (z1 + z2) * 0.5
            nl = math.sqrt(my*my + mz*mz) or 1.0
            n = b.add_normal(0, my / nl, mz / nl)
            b.add_quad(s_lv[i], s_lv[i+1], s_rv[i+1], s_rv[i], n, [uv0, uv3, uv2, uv1])

    # h) Hinges on Back (2 cylindrical cylinders / boxes)
    for hx in [-0.19, 0.19]:
        b.add_box(hx - 0.035, hx + 0.035, 0.365, 0.405, 0.265, 0.295, group="Iron")

    # i) Side drop handles (Left & Right)
    for side_x, sign in [(-0.42, -1), (0.42, 1)]:
        # Base plate
        b.add_box(side_x if sign < 0 else side_x - 0.012, 
                  side_x + 0.012 if sign < 0 else side_x, 
                  0.18, 0.24, -0.06, 0.06, group="Iron")
        # Handle ring (3 small segments)
        hx_out = side_x + sign * 0.04
        # Horizontal grip
        b.add_box(hx_out - 0.01, hx_out + 0.01, 0.13, 0.15, -0.05, 0.05, group="Iron")
        # Vertical drop arms
        b.add_box(side_x, hx_out, 0.14, 0.21, -0.05, -0.035, group="Iron")
        b.add_box(side_x, hx_out, 0.14, 0.21, 0.035, 0.05, group="Iron")

    # j) Iron Latch Tongue (descending from lid over the lock)
    b.add_box(-0.04, 0.04, 0.34, 0.43, -0.30, -0.28, group="Iron")

    # 4. GOLD DETAILS (GOLD)
    # a) Escutcheon / Lock Plate on front center
    b.add_box(-0.065, 0.065, 0.24, 0.35, -0.295, -0.28, group="Gold")
    # b) Padlock Body / Buckle
    b.add_box(-0.035, 0.035, 0.27, 0.33, -0.315, -0.295, group="Gold")
    # c) Keyhole (Iron cutout inset)
    b.add_box(-0.008, 0.008, 0.285, 0.315, -0.318, -0.312, group="Iron")

    # Rivets (Small Iron studs on corners and straps)
    rivet_size = 0.012
    # Straps front rivets
    for rx in [-0.19, 0.19]:
        for ry in [0.03, 0.18, 0.36]:
            b.add_box(rx - rivet_size, rx + rivet_size, ry - rivet_size, ry + rivet_size, -0.285, -0.275, group="Iron")
        # Straps back rivets
        for ry in [0.03, 0.18, 0.36]:
            b.add_box(rx - rivet_size, rx + rivet_size, ry - rivet_size, ry + rivet_size, 0.275, 0.285, group="Iron")

    # Corners rivets
    for cx in [-0.39, 0.39]:
        for cz, sgnz in [(-0.25, -1), (0.25, 1)]:
            z_pos = -0.285 if sgnz < 0 else 0.275
            for ry in [0.03, 0.36]:
                b.add_box(cx - rivet_size, cx + rivet_size, ry - rivet_size, ry + rivet_size, z_pos, z_pos + 0.01, group="Iron")

    return b

if __name__ == "__main__":
    builder = build_chest()
    obj_path = r"c:\Users\linco\Mugg\Assets\_Duskborn\Art\Models\Chest.obj"
    mtl_path = r"c:\Users\linco\Mugg\Assets\_Duskborn\Art\Models\Chest.mtl"
    builder.export_obj(obj_path, "Chest.mtl")
    
    with open(mtl_path, "w", encoding="utf-8") as f:
        f.write("# Duskborn Chest Material Library\n")
        f.write("newmtl Wood\nKd 0.63 0.48 0.35\n\n")
        f.write("newmtl Iron\nKd 0.22 0.24 0.27\n\n")
        f.write("newmtl Gold\nKd 0.85 0.70 0.20\n\n")
        
    print(f"Generated {obj_path} with {len(builder.vertices)} vertices and {sum(len(faces) for faces in builder.groups.values())} triangles.")
