import math

class ObjBuilder:
    def __init__(self):
        self.vertices = []
        self.normals = []
        self.uvs = []
        self.groups = {}
        self.current_group = "default"

    def set_group(self, name):
        self.current_group = name
        if name not in self.groups:
            self.groups[name] = []

    def add_vertex(self, x, y, z):
        self.vertices.append((x, y, z))
        return len(self.vertices)

    def add_normal(self, nx, ny, nz):
        l = math.sqrt(nx*nx + ny*ny + nz*nz) or 1.0
        self.normals.append((nx/l, ny/l, nz/l))
        return len(self.normals)

    def add_uv(self, u, v):
        self.uvs.append((u, v))
        return len(self.uvs)

    def add_quad(self, v1, v2, v3, v4, normal_idx=None, uvs=None):
        u = uvs if uvs else [1, 1, 1, 1]
        if normal_idx is None:
            # Compute facet normal from v1, v2, v3
            p1 = self.vertices[v1 - 1]
            p2 = self.vertices[v2 - 1]
            p3 = self.vertices[v3 - 1]
            ux, uy, uz = p2[0] - p1[0], p2[1] - p1[1], p2[2] - p1[2]
            vx, vy, vz = p3[0] - p1[0], p3[1] - p1[1], p3[2] - p1[2]
            nx = uy * vz - uz * vy
            ny = uz * vx - ux * vz
            nz = ux * vy - uy * vx
            normal_idx = self.add_normal(nx, ny, nz)

        self.groups[self.current_group].append(((v1, u[0], normal_idx), (v2, u[1], normal_idx), (v3, u[2], normal_idx)))
        self.groups[self.current_group].append(((v1, u[0], normal_idx), (v3, u[2], normal_idx), (v4, u[3], normal_idx)))

    def add_tri(self, v1, v2, v3, normal_idx=None, uvs=None):
        u = uvs if uvs else [1, 1, 1]
        if normal_idx is None:
            p1 = self.vertices[v1 - 1]
            p2 = self.vertices[v2 - 1]
            p3 = self.vertices[v3 - 1]
            ux, uy, uz = p2[0] - p1[0], p2[1] - p1[1], p2[2] - p1[2]
            vx, vy, vz = p3[0] - p1[0], p3[1] - p1[1], p3[2] - p1[2]
            nx = uy * vz - uz * vy
            ny = uz * vx - ux * vz
            nz = ux * vy - uy * vx
            normal_idx = self.add_normal(nx, ny, nz)

        self.groups[self.current_group].append(((v1, u[0], normal_idx), (v2, u[1], normal_idx), (v3, u[2], normal_idx)))


def build_pickaxe():
    b = ObjBuilder()

    # Default UVs
    u0 = b.add_uv(0, 0)
    u1 = b.add_uv(1, 0)
    u2 = b.add_uv(1, 1)
    u3 = b.add_uv(0, 1)
    uv_quad = [u0, u1, u2, u3]
    uv_tri = [u0, u1, u2]

    # =========================================================================
    # 1. WOODEN SHAFT / HANDLE (WOOD)
    # Coordinate system matches axe.fbx:
    # Handle extends along Z: Z=-0.96 (top) to Z=+1.00 (butt/grip base)
    # Forward swing direction is +Y.
    # Thickness in X is width.
    # =========================================================================
    b.set_group("Wood")

    shaft_levels = [
        (-0.96, 0.055, 0.060), # Top tip above pick head
        (-0.90, 0.058, 0.062), # Above head collar
        (-0.68, 0.058, 0.062), # Below head collar
        (0.00,  0.054, 0.058), # Upper mid handle
        (0.60,  0.052, 0.056), # Grip region start
        (0.92,  0.056, 0.060), # Grip end
        (0.96,  0.066, 0.070), # Pommel flare start
        (1.00,  0.072, 0.076), # Pommel base
    ]

    segs = 8
    rings = []
    for z, rx, ry in shaft_levels:
        ring = []
        for i in range(segs):
            angle = 2.0 * math.pi * i / segs + (math.pi / 8.0)
            x = rx * math.cos(angle)
            y = ry * math.sin(angle)
            ring.append(b.add_vertex(x, y, z))
        rings.append(ring)

    # Top end cap (Z = -0.96, facing -Z)
    top_center = b.add_vertex(0, 0, -0.96)
    n_top = b.add_normal(0, 0, -1)
    for i in range(segs):
        ni = (i + 1) % segs
        b.add_tri(top_center, rings[0][i], rings[0][ni], n_top, uv_tri)

    # Side quads connecting rings
    for r in range(len(rings) - 1):
        r1 = rings[r]
        r2 = rings[r + 1]
        for i in range(segs):
            ni = (i + 1) % segs
            b.add_quad(r1[i], r1[ni], r2[ni], r2[i], None, uv_quad)

    # Bottom end cap (Z = 1.00, facing +Z)
    bot_center = b.add_vertex(0, 0, 1.00)
    n_bot = b.add_normal(0, 0, 1)
    for i in range(segs):
        ni = (i + 1) % segs
        b.add_tri(bot_center, rings[-1][ni], rings[-1][i], n_bot, uv_tri)


    # =========================================================================
    # 2. PICKAXE HEAD (STONE)
    # Head mounted at Z = -0.80
    # =========================================================================
    b.set_group("Stone")

    # --- a) Center Collar ---
    collar_levels = [
        (-0.90, 0.076, 0.082),
        (-0.80, 0.082, 0.088),
        (-0.70, 0.076, 0.082),
    ]
    c_rings = []
    for z, rx, ry in collar_levels:
        ring = []
        for i in range(segs):
            angle = 2.0 * math.pi * i / segs + (math.pi / 8.0)
            x = rx * math.cos(angle)
            y = ry * math.sin(angle)
            ring.append(b.add_vertex(x, y, z))
        c_rings.append(ring)

    # Collar top cap ring
    for i in range(segs):
        ni = (i + 1) % segs
        b.add_quad(c_rings[0][ni], c_rings[0][i], rings[1][i], rings[1][ni], None, uv_quad)

    # Collar side faces
    for r in range(len(c_rings) - 1):
        r1 = c_rings[r]
        r2 = c_rings[r + 1]
        for i in range(segs):
            ni = (i + 1) % segs
            b.add_quad(r1[i], r1[ni], r2[ni], r2[i], None, uv_quad)

    # Collar bottom cap ring
    for i in range(segs):
        ni = (i + 1) % segs
        b.add_quad(rings[2][ni], rings[2][i], c_rings[-1][i], c_rings[-1][ni], None, uv_quad)


    # --- b) Forward Curved Pick Horn (+Y) ---
    pick_sections = [
        (0.08, -0.80, 0.065, 0.090),
        (0.22, -0.81, 0.055, 0.075),
        (0.38, -0.79, 0.042, 0.060),
        (0.52, -0.75, 0.030, 0.045),
        (0.64, -0.68, 0.018, 0.028),
    ]

    p_rings = []
    for y, z, hx, hz in pick_sections:
        v_l = b.add_vertex(-hx, y, z)
        v_t = b.add_vertex(0,   y, z - hz)
        v_r = b.add_vertex(hx,  y, z)
        v_b = b.add_vertex(0,   y, z + hz)
        p_rings.append([v_l, v_t, v_r, v_b])

    # Connect pick sections
    for r in range(len(p_rings) - 1):
        r1 = p_rings[r]
        r2 = p_rings[r + 1]
        b.add_quad(r1[0], r2[0], r2[1], r1[1], None, uv_quad)
        b.add_quad(r1[1], r2[1], r2[2], r1[2], None, uv_quad)
        b.add_quad(r1[2], r2[2], r2[3], r1[3], None, uv_quad)
        b.add_quad(r1[3], r2[3], r2[0], r1[0], None, uv_quad)

    # Pick sharp tip point
    tip_v = b.add_vertex(0, 0.72, -0.62)
    last_r = p_rings[-1]
    b.add_tri(last_r[0], tip_v, last_r[1], None, uv_tri)
    b.add_tri(last_r[1], tip_v, last_r[2], None, uv_tri)
    b.add_tri(last_r[2], tip_v, last_r[3], None, uv_tri)
    b.add_tri(last_r[3], tip_v, last_r[0], None, uv_tri)


    # --- c) Rear Chisel / Hammer Poll (-Y) ---
    rear_sections = [
        (-0.08, -0.80, 0.065, 0.090),
        (-0.20, -0.80, 0.055, 0.080),
        (-0.32, -0.79, 0.045, 0.068),
        (-0.40, -0.78, 0.040, 0.058),
    ]

    rear_rings = []
    for y, z, hx, hz in rear_sections:
        v_l = b.add_vertex(-hx, y, z)
        v_t = b.add_vertex(0,   y, z - hz)
        v_r = b.add_vertex(hx,  y, z)
        v_b = b.add_vertex(0,   y, z + hz)
        rear_rings.append([v_l, v_t, v_r, v_b])

    # Connect rear sections
    for r in range(len(rear_rings) - 1):
        r1 = rear_rings[r]
        r2 = rear_rings[r + 1]
        b.add_quad(r1[1], r2[1], r2[0], r1[0], None, uv_quad)
        b.add_quad(r1[2], r2[2], r2[1], r1[1], None, uv_quad)
        b.add_quad(r1[3], r2[3], r2[2], r1[2], None, uv_quad)
        b.add_quad(r1[0], r2[0], r2[3], r1[3], None, uv_quad)

    # Flat hammer / chisel face at rear tip
    last_rear = rear_rings[-1]
    b.add_quad(last_rear[0], last_rear[3], last_rear[2], last_rear[1], None, uv_quad)

    return b

if __name__ == "__main__":
    builder = build_pickaxe()
    print(f"Generated pickaxe with {len(builder.vertices)} vertices and {sum(len(f) for f in builder.groups.values())} triangles.")
