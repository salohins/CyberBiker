"""Bake GunAnimations3 sword actions to the export rig and write animation-only FBXs.

Run with Blender 5.2+, --background --factory-startup --disable-autoexec SOURCE
--python tools/animations/export_sword_animations.py. The source file is never saved.
"""
import hashlib,json,math,sys
from pathlib import Path
import bpy
from bpy_extras import anim_utils
from mathutils import Matrix,Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Art/Animations/Sword'
LOG=ROOT/'Logs/sword_import'
OUT.mkdir(parents=True,exist_ok=True);LOG.mkdir(parents=True,exist_ok=True)
NAMES=['Player_Sword_Grab','Player_Sword_Back','Player_Sword_Idle','Player_Sword_Idle_Left','Player_Sword_Idle_Right','Player_Sword_Attack_Left','Player_Sword_Attack_Right']
scene=bpy.context.scene;source=bpy.data.objects['blenderRig'];target=bpy.data.objects['gameRig']
active=bpy.context.object
if active is not None and active.mode!='OBJECT':
    with bpy.context.temp_override(object=active,active_object=active,selected_objects=[active],selected_editable_objects=[active]):
        assert bpy.ops.object.mode_set(mode='OBJECT')=={'FINISHED'}
assert all(abs(v-1)<1e-6 for v in target.scale),'Export rig must have unit scale'
source.animation_data.use_tweak_mode=False
source.animation_data.use_nla=False
for track in source.animation_data.nla_tracks:track.mute=True
baseline={b.name:b.matrix_basis.copy() for b in source.pose.bones}
constraints={(b.name,c.name):(c.influence,c.mute) for b in source.pose.bones for c in b.constraints}
properties={b.name:{k:b[k] for k in b.keys() if isinstance(b[k],(int,float,bool))} for b in source.pose.bones}
samples={};manifest={'source':Path(bpy.data.filepath).name,'source_sha256':hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest(),'fps':scene.render.fps/scene.render.fps_base,'rig':'unityRig','boneCount':len(target.data.bones),'exportedMeshCount':0,'clips':[]}
for name in NAMES:
    source.animation_data.action=None
    for bone in source.pose.bones:
        bone.matrix_basis=baseline[bone.name]
        for key,value in properties[bone.name].items():bone[key]=value
        for c in bone.constraints:c.influence,c.mute=constraints[(bone.name,c.name)]
    action=bpy.data.actions[name];source.animation_data.action=action;source.animation_data.action_slot=action.slots[0]
    start,end=map(lambda v:int(round(v)),action.frame_range)
    curves={f.data_path:f for layer in action.layers for strip in layer.strips for bag in strip.channelbags for f in bag.fcurves}
    disabled=[]
    # Rigify's unused hand->gun parenting creates a dependency cycle in this
    # source. Remove only edges proven to have zero influence for this clip.
    for c in source.pose.bones['hand_ik.R'].constraints:
        if c.type!='CHILD_OF' or c.subtarget!='Gun_Main':continue
        path=c.path_from_id('influence');curve=curves.get(path)
        driven=any(d.data_path==path for d in source.animation_data.drivers)
        inactive=not driven and all(abs(curve.evaluate(f) if curve else c.influence)<1e-7 for f in range(start,end+1))
        if inactive:c.mute=True;disabled.append(c.name)
    frames=[]
    for frame in range(start,end+1):
        scene.frame_set(frame);bpy.context.view_layer.update()
        rig=target.evaluated_get(bpy.context.evaluated_depsgraph_get())
        pose={b.name:b.matrix.copy() for b in rig.pose.bones}
        assert all(math.isfinite(x) for m in pose.values() for row in m for x in row),name+' has non-finite poses'
        frames.append((frame,pose))
    samples[name]=frames
    pos_error=max((frames[0][1][b].translation-frames[-1][1][b].translation).length for b in frames[0][1])
    rot_error=max(math.degrees(frames[0][1][b].to_quaternion().rotation_difference(frames[-1][1][b].to_quaternion()).angle) for b in frames[0][1])
    manifest['clips'].append({'name':name,'firstFrame':start,'lastFrame':end,'length':(end-start)/manifest['fps'],'loop':('Idle' in name and pos_error<.002 and rot_error<1),'endpointPositionError':pos_error,'endpointAngleError':rot_error,'inactiveGunConstraintMuted':disabled})

# The target now plays only its baked transforms, with no control-rig dependency.
source.animation_data.action=None
for bone in source.pose.bones:
    for c in bone.constraints:c.mute=True
for bone in target.pose.bones:
    for c in bone.constraints:c.mute=True
target.animation_data_create();target.animation_data.use_nla=False
target.name='unityRig'
def expose(layer):
    layer.exclude=False;layer.hide_viewport=False
    for child in layer.children:expose(child)
expose(bpy.context.view_layer.layer_collection)
for collection in target.users_collection:collection.hide_viewport=False
target.hide_viewport=False;target.hide_render=False;target.hide_select=False;target.hide_set(False)
for bone in target.pose.bones:bone.rotation_mode='QUATERNION'
baked={}
for rec in manifest['clips']:
    name=rec['name'];frames=samples[name]
    action=bpy.data.actions.new('UNITY_'+name);action.use_fake_user=True
    slot=action.slots.new(id_type='OBJECT',name=target.name)
    bag=anim_utils.action_ensure_channelbag_for_slot(action,slot)
    for bone in target.pose.bones:
        rows=[];previous=None
        for frame,pose in frames:
            kw={'matrix':pose[bone.name],'matrix_local':bone.bone.matrix_local,'invert':True}
            if bone.parent:kw.update(parent_matrix=pose[bone.parent.name],parent_matrix_local=bone.parent.bone.matrix_local)
            basis=bone.bone.convert_local_to_pose(**kw);location,rotation,scale=basis.decompose()
            if previous is not None and previous.dot(rotation)<0:rotation.negate()
            previous=rotation.copy();rows.append((frame,list(location),list(rotation),list(scale)))
        for prop,column,count in [('location',1,3),('rotation_quaternion',2,4),('scale',3,3)]:
            for index in range(count):
                curve=bag.fcurves.ensure(bone.path_from_id(prop),index=index,group_name=bone.name)
                curve.keyframe_points.add(len(rows));curve.keyframe_points.foreach_set('co',[v for row in rows for v in (row[0],row[column][index])])
                for key in curve.keyframe_points:key.interpolation='LINEAR'
                curve.update()
    target.animation_data.action=action;target.animation_data.action_slot=slot
    max_pos=max_angle=0
    for frame,pose in frames:
        scene.frame_set(frame);bpy.context.view_layer.update();rig=target.evaluated_get(bpy.context.evaluated_depsgraph_get())
        for bone in rig.pose.bones:
            max_pos=max(max_pos,(bone.matrix.translation-pose[bone.name].translation).length)
            max_angle=max(max_angle,math.degrees(bone.matrix.to_quaternion().rotation_difference(pose[bone.name].to_quaternion()).angle))
    assert max_pos<.0001 and max_angle<.1,(name,max_pos,max_angle)
    rec.update(bakePositionError=max_pos,bakeAngleError=max_angle)
    baked[name]=action
    print('BAKED',name,'position error',max_pos,'angle error',max_angle,flush=True)

references={'boneNames':[b.name for b in target.data.bones],'clips':[]}
for rec in manifest['clips']:
    name=rec['name'];target.animation_data.action=baked[name];target.animation_data.action_slot=baked[name].slots[0]
    scene.frame_start=rec['firstFrame'];scene.frame_end=rec['lastFrame'];scene.frame_set(scene.frame_start)
    for ob in bpy.context.view_layer.objects:ob.select_set(False)
    target.select_set(True);bpy.context.view_layer.objects.active=target
    assert target in bpy.context.selected_objects,'Export skeleton is not selected/visible'
    path=OUT/'FBX'/(name+'.fbx');path.parent.mkdir(exist_ok=True)
    result=bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,
        armature_nodetype='NULL',use_armature_deform_only=False,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',axis_up='Y',bake_space_transform=False,bake_anim=True,bake_anim_use_all_bones=True,
        bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_step=1,
        bake_anim_force_startend_keying=True,use_custom_props=False)
    assert result=={'FINISHED'} and path.stat().st_size>50000,'Empty FBX export: '+str(path)
    rec['file']='FBX/'+path.name;rec['fileBytes']=path.stat().st_size
    refs=[]
    chosen={rec['firstFrame'],rec['lastFrame'],round((rec['firstFrame']+rec['lastFrame'])/2)}
    for frame,pose in samples[name]:
        if frame not in chosen:continue
        refs.append({'frame':frame,'bones':[{'name':n,'position':dict(zip('xyz',pose[n].translation)),'rotation':dict(zip('xyzw',(pose[n].to_quaternion().x,pose[n].to_quaternion().y,pose[n].to_quaternion().z,pose[n].to_quaternion().w)))} for n in pose]})
    references['clips'].append({'name':name,'samples':refs})

(OUT/'SwordAnimations.manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
(ROOT/'SourceAssets/Animations/SwordPoseReference.json').write_text(json.dumps(references,indent=2)+'\n',encoding='utf-8')
# Derive the approach position from the real blade arc instead of a generic
# side distance. Use a point inside the blade, so the cut intersects the target.
profiles={}
for side in ('Left','Right'):
    rec=next(c for c in manifest['clips'] if c['name']=='Player_Sword_Attack_'+side)
    rows=[]
    for frame,pose in samples[rec['name']]:
        point=pose['Sword_Main']@Vector((0,-.82,0))
        rows.append({'frame':frame,'point':dict(zip('xyz',(-point.x,point.z,-point.y)))})
    candidates=[row for row in rows if rec['firstFrame']+7<=row['frame']<=rec['lastFrame']-6]
    contact=max(candidates,key=lambda row:row['point']['x']*(1 if side=='Right' else -1))
    profiles[side]={'contactTime':(contact['frame']-rec['firstFrame'])/(rec['lastFrame']-rec['firstFrame']),'contactPoint':contact['point'],'samples':rows}
(ROOT/'SourceAssets/Animations/SwordBladeTrajectories.json').write_text(json.dumps(profiles,indent=2)+'\n',encoding='utf-8')
working=LOG/'SwordAnimations_Baked.blend'
bpy.data.use_autopack=False
bpy.ops.wm.save_as_mainfile(filepath=str(working),copy=True)
print('@@JSON@@'+json.dumps({'ok':True,'clips':manifest['clips'],'boneCount':manifest['boneCount'],'workingCopy':str(working)}))
