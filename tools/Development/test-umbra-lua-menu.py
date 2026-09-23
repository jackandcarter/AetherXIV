#!/usr/bin/env python3
"""Exercise the production menu callbacks against a real Lua 5.1 VM.

Pass --lua-source /path/to/lua-5.1.5 (build src/liblua.a first). This tests
callback/stack/error semantics, not the client's native UI or x86 addresses.
"""
import argparse
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'AetherXIV Launcher/Umbra/Aether.Umbra.Bootstrap/UmbraLuaMainMenu.inl'
TEST = r'''
local function widget(count, fail)
  local w = {count=count or 19, rows={}, initCalls=0, stockCalls=0, inserts=0, hidden=false}
  for i=0,w.count-1 do w.rows[i]={MainName='stock'..i} end
  function w:init(...) self.initCalls=self.initCalls+1; return 'init-result',nil,17 end
  function w:processUICommandSelectionChanged(a,control,row,b)
    if row == 5 then error('stock-error') end
    self.stockCalls=self.stockCalls+1; self.last={a,control,row,b}; return 'selected',row
  end
  function w:getListPropertyCount(control) assert(control=='MainMenu'); return self.count end
  function w:insertListProperty(list,row)
    assert(list=='MainMenu' and (row==19 or row==20) and self.count==row)
    self.count=self.count+1; self.rows[row]={}; self.inserts=self.inserts+1
  end
  function w:setListProperty(list,row,key,value)
    assert(list=='MainMenu' and (row==19 or row==20))
    if key==fail or (key..row)==fail then error('property-failure') end
    self.rows[row][key]=value
  end
  function w:getListProperty(list,row,key) assert(list=='MainMenu'); return self.rows[row][key] end
  function w:updateListProperty(list) assert(list=='MainMenu') end
  function w:deleteListProperty(list,row)
    assert(list=='MainMenu' and (row==19 or row==20) and self.count==row+1)
    self.rows[row]=nil; self.count=self.count-1
  end
  function w:hide() self.hidden=true end
  wrap(w)
  return w
end
local w=widget()
local a,b,c=w:init()
assert(a=='init-result' and b==nil and c==17)
assert(w.count==21 and w.rows[20].MainName=='Umbra Settings' and w.rows[19].MainName=='Umbra Plugin Manager' and w.rows[19].MainEnable=='True')
for i=0,18 do assert(w.rows[i].MainName=='stock'..i) end
w:init(); assert(w.inserts==2 and w.initCalls==2)
local a,b=w:processUICommandSelectionChanged('a','ListBox_MainMenu',0,'b')
assert(a=='selected' and b==0 and not w.hidden and pending()==0)
assert(w.last[1]=='a' and w.last[4]=='b')
w:processUICommandSelectionChanged('a','ListBox_SystemMenu',19,'b')
assert(not w.hidden and pending()==0)
local a,b=w:processUICommandSelectionChanged('a','ListBox_MainMenu',19,'b')
assert(a=='selected' and b==19 and w.hidden and pending()==1)
w.hidden=false
w:processUICommandSelectionChanged('a','ListBox_MainMenu',20,'b')
assert(w.hidden and pending()==2)
local ok,err=pcall(w.processUICommandSelectionChanged,w,'a','ListBox_MainMenu',5,'b')
assert(not ok and string.find(err,'stock%-error'))
for _,field in ipairs({'MainName','MainEnable','visibility','MainName20','MainEnable20','visibility20'}) do
  local broken=widget(19,field)
  local a,b,c=broken:init()
  assert(a=='init-result' and b==nil and c==17)
  assert(broken.count==19 and broken.rows[19]==nil)
  for i=0,18 do assert(broken.rows[i].MainName=='stock'..i) end
end
local other=widget(20); other:init(); assert(other.inserts==0 and other.rows[19].MainName=='stock19')
other:processUICommandSelectionChanged('a','ListBox_MainMenu',19,'b')
assert(not other.hidden and pending()==0)
local fewer=widget(18); fewer:init(); assert(fewer.inserts==0 and fewer.count==18)
collectgarbage('collect')
w:processUICommandSelectionChanged('a','ListBox_MainMenu',1,'b')
assert(w.last[3]==1)
local map={}
function map:init(mode) assert(mode==2); return 'map-init',nil,9 end
function map:processClosing(fail) if fail then error('close-error') end; return 'closed' end
wrap_map(map)
assert(map_active()==0)
local a,b,c=map:init(2)
assert(a=='map-init' and b==nil and c==9 and map_active()==1)
collectgarbage('collect')
assert(map:processClosing()=='closed' and map_active()==0)
map:init(2)
local ok,err=pcall(map.processClosing,map,true)
assert(not ok and string.find(err,'close-error',1,true) and map_active()==0)
local ok=pcall(map.init,map,99)
assert(not ok and map_active()==0)
print('PASS: menu routing/rollback/GC, map open/close state, result preservation and errors')
'''

def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--lua-source', required=True, type=Path)
    args=ap.parse_args()
    s=SOURCE.read_text()
    api=s[s.index('    struct Api'):s.index('    Call originalCall')]
    logic=s[s.index('    void Pop'):s.index('    bool PushWrappedMethod')]
    lifecycle=s[s.index('    namespace MapLifecycle'):s.index('        int __cdecl Bind',s.index('    namespace MapLifecycle'))] + '\n}\n'
    names=re.findall(r'\(__cdecl\* (\w+)\)',api)
    assignments='\n'.join(f'api.{name}=reinterpret_cast<decltype(api.{name})>(lua_{name});' for name in names)
    cpp=r'''
#include <cstdio>
#include <cstring>
extern "C" {
#include "lua.h"
#include "lauxlib.h"
#include "lualib.h"
}
#define __cdecl
using State=lua_State;
using Callback=lua_CFunction;
using LONG=long;
long InterlockedIncrement(volatile long* n) { return ++*n; }
long InterlockedExchange(volatile long* n,long v) { long old=*n; *n=v; return old; }
int lstrcmpA(const char* a,const char* b) { return strcmp(a,b); }
volatile LONG rootHits=0,bound=0,rowHits=0,selectionHits=0,errors=0,openPending=0,observedCount=-1;
''' + api + logic + lifecycle + r'''
int WrapMap(State* L) {
  lua_getfield(L,1,"init"); lua_pushcclosure(L,MapLifecycle::Init,1); lua_setfield(L,1,"init");
  lua_getfield(L,1,"processClosing"); lua_pushcclosure(L,MapLifecycle::Closing,1); lua_setfield(L,1,"processClosing");
  return 0;
}
int MapActive(State* L) { lua_pushnumber(L,MapLifecycle::active); return 1; }
int Wrap(State* L) {
  lua_getfield(L,1,"init"); lua_pushcclosure(L,Init,1); lua_setfield(L,1,"init");
  lua_getfield(L,1,"processUICommandSelectionChanged");
  lua_pushcclosure(L,Selection,1); lua_setfield(L,1,"processUICommandSelectionChanged");
  return 0;
}
int Pending(State* L) { lua_pushnumber(L,openPending); openPending=0; return 1; }
int main(int argc,char** argv) {
''' + assignments + r'''
  State* L=luaL_newstate(); luaL_openlibs(L);
  lua_register(L,"wrap",Wrap); lua_register(L,"pending",Pending);
  lua_register(L,"wrap_map",WrapMap); lua_register(L,"map_active",MapActive);
  if(luaL_dofile(L,argv[1])) { fprintf(stderr,"%s\n",lua_tostring(L,-1)); return 1; }
  if(lua_gettop(L)!=0) { fprintf(stderr,"unbalanced stack\n"); return 1; }
  lua_close(L);
}
'''
    with tempfile.TemporaryDirectory(prefix='umbra-lua-menu-test-') as tmp:
        tmp=Path(tmp); (tmp/'test.cpp').write_text(cpp); (tmp/'test.lua').write_text(TEST)
        subprocess.run(['c++','-std=c++17','-I'+str(args.lua_source/'src'),str(tmp/'test.cpp'),str(args.lua_source/'src/liblua.a'),'-o',str(tmp/'test')],check=True)
        subprocess.run([str(tmp/'test'),str(tmp/'test.lua')],check=True)
if __name__=='__main__': main()
