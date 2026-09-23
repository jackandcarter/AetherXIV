#!/usr/bin/env python3
"""Check production overlay target switching/restoration with a simulated DX9 device."""
from pathlib import Path
import tempfile, subprocess
root = Path(__file__).resolve().parents[2]
s = (root/'AetherXIV Launcher/Umbra/Aether.Umbra.Bootstrap/dllmain.cpp').read_text()
a=s.index('    void RenderUmbraOverlay(IDirect3DDevice9* device,')
b=s.index('    void RenderUmbraOverlayContents(IDirect3DDevice9* device)\n',a)
logic=s[a:b]
harness=r'''
#include <cassert>
#include <cstdio>
using DWORD=unsigned long; using HRESULT=int;
#define SUCCEEDED(x) ((x)>=0)
#define FAILED(x) ((x)<0)
constexpr int D3DBACKBUFFER_TYPE_MONO=0;
struct D3DSURFACE_DESC { DWORD Width,Height; };
struct D3DVIEWPORT9 { DWORD X,Y,Width,Height; float MinZ,MaxZ; };
struct D3DCAPS9 { DWORD NumSimultaneousRTs; };
struct IDirect3DSurface9 {
 int refs=0; HRESULT GetDesc(D3DSURFACE_DESC* d){*d={1920,1080}; return 0;}
 void Release(){--refs;}
};
struct IDirect3DDevice9 {
 IDirect3DSurface9 original[5],back;
 IDirect3DSurface9* targets[4]={&original[0],&original[1],&original[2],&original[3]};
 IDirect3DSurface9* depth=&original[4]; D3DVIEWPORT9 viewport{0,0,1,1,0,1};
 bool failBegin=false; int ended=0,drawn=0;
 HRESULT GetBackBuffer(int,int,int,IDirect3DSurface9** v){*v=&back;++back.refs;return 0;}
 HRESULT GetViewport(D3DVIEWPORT9* v){*v=viewport;return 0;}
 HRESULT GetDeviceCaps(D3DCAPS9* v){v->NumSimultaneousRTs=4;return 0;}
 HRESULT GetRenderTarget(DWORD i,IDirect3DSurface9** v){*v=targets[i];if(*v)++(*v)->refs;return 0;}
 HRESULT GetDepthStencilSurface(IDirect3DSurface9** v){*v=depth;if(*v)++(*v)->refs;return 0;}
 HRESULT SetDepthStencilSurface(IDirect3DSurface9* v){depth=v;return 0;}
 HRESULT SetRenderTarget(DWORD i,IDirect3DSurface9* v){targets[i]=v;return 0;}
 HRESULT SetViewport(D3DVIEWPORT9* v){viewport=*v;return 0;}
 HRESULT BeginScene(){return failBegin?-1:0;}
 HRESULT EndScene(){++ended;return 0;}
};
struct IDirect3DSwapChain9 {
 IDirect3DDevice9* device; HRESULT GetBackBuffer(int,int,IDirect3DSurface9** v){return device->GetBackBuffer(0,0,0,v);}
};
HRESULT (*OriginalEndScene)(IDirect3DDevice9*)=nullptr;
void RenderUmbraOverlayContents(IDirect3DDevice9* d){
 assert(d->targets[0]==&d->back && !d->depth);
 for(int i=1;i<4;++i)assert(!d->targets[i]);
 assert(d->viewport.Width==1920 && d->viewport.Height==1080); ++d->drawn;
}
'''
harness+=logic+r'''
int main(){
 for(bool fail:{false,true}) {
  IDirect3DDevice9 d;d.failBegin=fail;IDirect3DSwapChain9 swap{&d};
  RenderUmbraOverlay(&d,&swap);
  assert(d.drawn==(fail?0:1) && d.ended==(fail?0:1));
  assert(d.viewport.Width==1 && d.viewport.Height==1 && d.depth==&d.original[4]);
  for(int i=0;i<4;++i)assert(d.targets[i]==&d.original[i]);
  for(auto& surface:d.original)assert(surface.refs==0);assert(d.back.refs==0);
 }
 puts("PASS: presented backbuffer, full viewport, MRT/depth restoration, balanced scenes and references, BeginScene failure");
}
'''
harness='#include <initializer_list>\n'+harness
with tempfile.TemporaryDirectory(prefix='umbra-overlay-test-') as t:
 p=Path(t);(p/'test.cpp').write_text(harness)
 subprocess.run(['c++','-std=c++17',str(p/'test.cpp'),'-o',str(p/'test')],check=True)
 subprocess.run([str(p/'test')],check=True)
