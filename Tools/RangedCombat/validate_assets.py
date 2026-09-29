"""Offline checks of the saved mesh bytes and Unity references (no Unity import claim)."""
from pathlib import Path
import math, re, struct, unittest

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/_Duskborn/Art/Models/WoodenBow'
DATA=ROOT/'Assets/_Duskborn/Resources/Weapons'

class RangedAssetTests(unittest.TestCase):
    def test_mesh_payloads_and_bounds(self):
        for path in ART.glob('*.asset'):
            with self.subTest(mesh=path.name):
                text=path.read_text()
                count=int(re.search(r'm_VertexCount: (\d+)',text)[1])
                data=bytes.fromhex(re.search(r'_typelessdata: (\w+)',text)[1])
                indices=bytes.fromhex(re.search(r'm_IndexBuffer: (\w+)',text)[1])
                self.assertEqual(len(data),count*24)
                self.assertEqual(len(indices),count*2)
                self.assertEqual(count%3,0)
                center=[float(x) for x in re.search(r'm_Center: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)',text).groups()]
                extent=[float(x) for x in re.search(r'm_Extent: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)',text).groups()]
                for vertex in struct.iter_unpack('<6f',data):
                    self.assertTrue(all(math.isfinite(x) for x in vertex))
                    self.assertAlmostEqual(sum(x*x for x in vertex[3:]),1,places=5)
                    for axis in range(3): self.assertLessEqual(abs(vertex[axis]-center[axis]),extent[axis]+1e-6)
                self.assertEqual(list(struct.unpack('<'+'H'*count,indices)),list(range(count)))

    def test_references_resolve(self):
        # Includes imported third-party animation metadata, even though Git ignores it.
        known={}
        for path in (ROOT/'Assets').rglob('*.meta'):
            text=path.read_text(errors='replace')
            match=re.search(r'^guid: (\w+)',text,re.M)
            if match: known[match[1]]=path
        paths=list(ART.glob('*.prefab'))+list(DATA.glob('*.asset'))
        paths.append(ROOT/'Assets/_Duskborn/Prefabs/Enemies/ArcherTest.prefab')
        for path in paths:
            for guid in re.findall(r'guid: (\w+)',path.read_text()):
                self.assertIn(guid,known,f'{path.name}: unresolved GUID {guid}')

    def test_equipment_and_recipe_wiring(self):
        bow=DATA/'weapon_wooden_bow.asset'
        guid=re.search(r'guid: (\w+)',Path(str(bow)+'.meta').read_text())[1]
        for path in ['Resources/Inventory/InitialInventoryDatabase.asset','Resources/Crafting/Recipe_SimpleBow.asset','Prefabs/Enemies/ArcherTest.prefab']:
            self.assertIn(guid,(ROOT/'Assets/_Duskborn'/path).read_text())
        self.assertIn('bone: 17',(DATA/'WoodenBowLeftHand.asset').read_text())
        self.assertEqual(bow.read_text().count('Type: 2'),1)
        self.assertIn('NormalizedTime: 0.67418647',bow.read_text())
        for path in ART.glob('*.prefab'):
            text=path.read_text()
            ids=re.findall(r'^--- !u!\d+ &(-?\d+)',text,re.M)
            self.assertEqual(len(ids),len(set(ids)))
            for local in re.findall(r'\{fileID: (-?\d+)\}',text):
                self.assertTrue(local=='0' or local in ids,f'{path}: missing local object {local}')

if __name__=='__main__': unittest.main(verbosity=2)
