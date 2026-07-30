"""Inspect FBX model and animation structure from Blender in background mode."""

import bpy
import hashlib
import json
import os
import sys


def reset_scene():
    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.actions, bpy.data.armatures, bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for datablock in list(datablocks):
            datablocks.remove(datablock)


def inspect(path):
    reset_scene()
    if hasattr(bpy.ops.wm, "fbx_import"):
        bpy.ops.wm.fbx_import(filepath=path)
    else:
        bpy.ops.import_scene.fbx(filepath=path)

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    bones = sorted({bone.name for obj in armatures for bone in obj.data.bones})
    fingerprint = hashlib.sha256("\n".join(bones).encode("utf-8")).hexdigest() if bones else None
    fps = bpy.context.scene.render.fps / bpy.context.scene.render.fps_base

    actions = []
    for action in bpy.data.actions:
        start, end = action.frame_range
        actions.append(
            {
                "name": action.name,
                "frame_start": round(start, 3),
                "frame_end": round(end, 3),
                "frames": round(end - start + 1, 3),
                "duration_seconds": round((end - start) / fps, 4),
            }
        )

    images = [
        {
            "name": image.name,
            "width": image.size[0],
            "height": image.size[1],
            "packed": image.packed_file is not None,
        }
        for image in bpy.data.images
        if image.name not in {"Render Result", "Viewer Node"}
    ]

    return {
        "file": os.path.basename(path),
        "bytes": os.path.getsize(path),
        "armatures": len(armatures),
        "meshes": len(meshes),
        "vertices": sum(len(obj.data.vertices) for obj in meshes),
        "polygons": sum(len(obj.data.polygons) for obj in meshes),
        "bones": len(bones),
        "skeleton_sha256": fingerprint,
        "actions": actions,
        "images": images,
    }


paths = sys.argv[sys.argv.index("--") + 1:]
if not paths:
    raise SystemExit("Pass one or more FBX paths after --")

for fbx_path in paths:
    print("FBX_INSPECT=" + json.dumps(inspect(os.path.abspath(fbx_path)), sort_keys=True))
