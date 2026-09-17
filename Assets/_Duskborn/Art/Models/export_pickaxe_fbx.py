import math
from generate_pickaxe import build_pickaxe

def export_fbx():
    builder = build_pickaxe()
    
    verts = builder.vertices
    vert_str_list = []
    for v in verts:
        vert_str_list.append(f"{v[0]:.5f},{v[1]:.5f},{v[2]:.5f}")
    
    mat_map = {"Wood": 0, "Stone": 1}
    poly_indices = []
    normal_list = []
    uv_list = []
    mat_indices = []
    
    for group_name in ["Wood", "Stone"]:
        mat_id = mat_map[group_name]
        faces = builder.groups.get(group_name, [])
        for tri in faces:
            idx0 = tri[0][0] - 1
            idx1 = tri[1][0] - 1
            idx2 = tri[2][0] - 1
            
            # FBX PolygonVertexIndex: last vertex of poly is negated as: -(idx + 1)
            poly_indices.append(str(idx0))
            poly_indices.append(str(idx1))
            poly_indices.append(str(-(idx2 + 1)))
            
            for corner in tri:
                n = builder.normals[corner[2] - 1]
                normal_list.append(f"{n[0]:.5f},{n[1]:.5f},{n[2]:.5f}")
            
            for corner in tri:
                uv = builder.uvs[corner[1] - 1]
                uv_list.append(f"{uv[0]:.5f},{uv[1]:.5f}")
                
            mat_indices.append(str(mat_id))

    fbx_content = f"""; FBX 6.1.0 project file
; Generated for Duskborn
; ----------------------------------------------------

FBXHeaderExtension:  {{
	FBXHeaderVersion: 1003
	FBXVersion: 6100
	CreationTimeStamp:  {{
		Version: 1000
		Year: 2026
		Month: 09
		Day: 17
		Hour: 10
		Minute: 00
		Second: 00
		Millisecond: 0
	}}
	Creator: "Duskborn Custom Exporter"
	OtherFlags:  {{
		FlagPLE: 0
	}}
}}
CreationTime: "2026-09-17 10:00:00:000"
Creator: "Duskborn Exporter"

; Object definitions
;------------------------------------------------------------------

Definitions:  {{
	Version: 100
	Count: 5
	ObjectType: "Model" {{
		Count: 1
	}}
	ObjectType: "Geometry" {{
		Count: 1
	}}
	ObjectType: "Material" {{
		Count: 2
	}}
	ObjectType: "GlobalSettings" {{
		Count: 1
	}}
}}

; Object properties
;------------------------------------------------------------------

Objects:  {{
	Model: "Model::pickaxe", "Mesh" {{
		Version: 232
		Properties60:  {{
			Property: "QuaternionInterpolate", "bool", "",0
			Property: "Visibility", "Visibility", "A+",1
			Property: "Lcl Translation", "Lcl Translation", "A+",0,0,0
			Property: "Lcl Rotation", "Lcl Rotation", "A+",0,0,0
			Property: "Lcl Scaling", "Lcl Scaling", "A+",1,1,1
		}}
		MultiLayer: 0
		MultiTake: 1
		Shading: Y
		Culling: "CullingOff"
		Vertices: {",".join(vert_str_list)}
		PolygonVertexIndex: {",".join(poly_indices)}
		GeometryVersion: 124
		LayerElementNormal: 0 {{
			Version: 101
			Name: ""
			MappingInformationType: "ByPolygonVertex"
			ReferenceInformationType: "Direct"
			Normals: {",".join(normal_list)}
		}}
		LayerElementUV: 0 {{
			Version: 101
			Name: "UVMap"
			MappingInformationType: "ByPolygonVertex"
			ReferenceInformationType: "Direct"
			UV: {",".join(uv_list)}
		}}
		LayerElementMaterial: 0 {{
			Version: 101
			Name: ""
			MappingInformationType: "ByPolygon"
			ReferenceInformationType: "IndexToDirect"
			Materials: {",".join(mat_indices)}
		}}
		Layer: 0 {{
			Version: 100
			LayerElement:  {{
				Type: "LayerElementNormal"
				TypedIndex: 0
			}}
			LayerElement:  {{
				Type: "LayerElementUV"
				TypedIndex: 0
			}}
			LayerElement:  {{
				Type: "LayerElementMaterial"
				TypedIndex: 0
			}}
		}}
	}}
	Material: "Material::Wood", "" {{
		Version: 102
		ShadingModel: "lambert"
		MultiLayer: 0
		Properties60:  {{
			Property: "DiffuseColor", "ColorRGB", "",0.63,0.48,0.35
		}}
	}}
	Material: "Material::Stone", "" {{
		Version: 102
		ShadingModel: "lambert"
		MultiLayer: 0
		Properties60:  {{
			Property: "DiffuseColor", "ColorRGB", "",0.50,0.50,0.50
		}}
	}}
	GlobalSettings:  {{
		Version: 1000
		Properties60:  {{
			Property: "UpAxis", "int", "",1
			Property: "UpAxisSign", "int", "",1
			Property: "FrontAxis", "int", "",2
			Property: "FrontAxisSign", "int", "",1
			Property: "CoordAxis", "int", "",0
			Property: "CoordAxisSign", "int", "",1
			Property: "UnitScaleFactor", "double", "",1
		}}
	}}
}}

; Object relations
;------------------------------------------------------------------

Relations:  {{
	Model: "Model::pickaxe", "Mesh" {{
	}}
	Material: "Material::Wood", "" {{
	}}
	Material: "Material::Stone", "" {{
	}}
}}

; Object connections
;------------------------------------------------------------------

Connections:  {{
	Connect: "OO", "Model::pickaxe", "Model::Scene"
	Connect: "OO", "Material::Wood", "Model::pickaxe"
	Connect: "OO", "Material::Stone", "Model::pickaxe"
}}
"""
    fbx_path = r"Assets\_Duskborn\Art\Models\pickaxe.fbx"
    with open(fbx_path, "w", encoding="utf-8") as f:
        f.write(fbx_content)
    print(f"Exported pickaxe.fbx with {len(verts)} vertices and {len(mat_indices)} polygons.")

if __name__ == "__main__":
    export_fbx()
