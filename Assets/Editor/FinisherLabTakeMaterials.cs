using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberBiker.EditorTools {
    // Material instances are owned by the take; source assets are never edited.
    internal sealed class FinisherLabTakeMaterials : IDisposable {
        internal sealed class State {public int id,queue;public Vector4[] values;public Texture[] textures;public string[] keywords;}
        private sealed class Entry {public Material source,copy;public int[] ids;public ShaderPropertyType[] types;}
        private readonly Dictionary<Material,int> lookup=new Dictionary<Material,int>();
        private readonly List<Entry> entries=new List<Entry>();
        public int Register(Material source) {
            if(source==null)return -1;
            if(lookup.TryGetValue(source,out int id))return id;
            var shader=source.shader;int count=shader.GetPropertyCount();var entry=new Entry{source=source,copy=new Material(source){hideFlags=HideFlags.HideAndDontSave},ids=new int[count],types=new ShaderPropertyType[count]};
            for(int i=0;i<count;i++){entry.ids[i]=shader.GetPropertyNameId(i);entry.types[i]=shader.GetPropertyType(i);}
            id=entries.Count;entries.Add(entry);lookup.Add(source,id);return id;
        }
        public State[] Capture(out long bytes) {
            var result=new List<State>(entries.Count);bytes=0;
            for(int id=0;id<entries.Count;id++) {
                var entry=entries[id];if(entry.source==null)continue;
                var state=new State{id=id,queue=entry.source.renderQueue,values=new Vector4[entry.ids.Length],textures=new Texture[entry.ids.Length],keywords=entry.source.shaderKeywords};
                for(int i=0;i<entry.ids.Length;i++) {
                    int property=entry.ids[i];
                    switch(entry.types[i]) {
                        case ShaderPropertyType.Color:state.values[i]=entry.source.GetColor(property);break;
                        case ShaderPropertyType.Vector:state.values[i]=entry.source.GetVector(property);break;
                        case ShaderPropertyType.Texture:
                            state.textures[i]=entry.source.GetTexture(property);var scale=entry.source.GetTextureScale(property);var offset=entry.source.GetTextureOffset(property);
                            state.values[i]=new Vector4(scale.x,scale.y,offset.x,offset.y);break;
                        case ShaderPropertyType.Int:state.values[i].x=entry.source.GetInteger(property);break;
                        default:state.values[i].x=entry.source.GetFloat(property);break;
                    }
                }
                bytes+=state.values.Length*24L+128;result.Add(state);
            }
            return result.ToArray();
        }
        public Material Get(int id)=>id>=0 ? entries[id].copy : null;
        public void Restore(State[] states) {
            foreach(var state in states) {
                var entry=entries[state.id];entry.copy.renderQueue=state.queue;entry.copy.shaderKeywords=state.keywords;
                for(int i=0;i<entry.ids.Length;i++) {
                    int property=entry.ids[i];var value=state.values[i];
                    switch(entry.types[i]) {
                        case ShaderPropertyType.Color:entry.copy.SetColor(property,value);break;
                        case ShaderPropertyType.Vector:entry.copy.SetVector(property,value);break;
                        case ShaderPropertyType.Texture:
                            entry.copy.SetTexture(property,state.textures[i]);entry.copy.SetTextureScale(property,new Vector2(value.x,value.y));entry.copy.SetTextureOffset(property,new Vector2(value.z,value.w));break;
                        case ShaderPropertyType.Int:entry.copy.SetInteger(property,Mathf.RoundToInt(value.x));break;
                        default:entry.copy.SetFloat(property,value.x);break;
                    }
                }
            }
        }
        public void Dispose(){foreach(var entry in entries)if(entry.copy!=null)UnityEngine.Object.DestroyImmediate(entry.copy);entries.Clear();lookup.Clear();}
    }
}
