#include <d3d9.h>
#include <stdio.h>
#include <windows.h>

static LRESULT CALLBACK probe_window_proc(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
{
    return DefWindowProcA(window, message, wparam, lparam);
}

int main(void)
{
    WNDCLASSA window_class = {0};
    window_class.lpfnWndProc = probe_window_proc;
    window_class.hInstance = GetModuleHandleA(NULL);
    window_class.lpszClassName = "AetherXivDxvkProbe";
    if (!RegisterClassA(&window_class))
    {
        fprintf(stderr, "register_window_failed=%lu\n", (unsigned long)GetLastError());
        return 10;
    }

    HWND window = CreateWindowExA(
        0,
        window_class.lpszClassName,
        "AetherXIV DXVK probe",
        WS_OVERLAPPEDWINDOW,
        0,
        0,
        16,
        16,
        NULL,
        NULL,
        window_class.hInstance,
        NULL);
    if (!window)
    {
        fprintf(stderr, "create_window_failed=%lu\n", (unsigned long)GetLastError());
        return 11;
    }

    IDirect3D9 *direct3d = Direct3DCreate9(D3D_SDK_VERSION);
    if (!direct3d)
    {
        fprintf(stderr, "direct3d_create_failed\n");
        DestroyWindow(window);
        return 20;
    }

    D3DPRESENT_PARAMETERS present = {0};
    present.Windowed = TRUE;
    present.SwapEffect = D3DSWAPEFFECT_DISCARD;
    present.hDeviceWindow = window;
    present.BackBufferFormat = D3DFMT_UNKNOWN;

    IDirect3DDevice9 *device = NULL;
    HRESULT result = IDirect3D9_CreateDevice(
        direct3d,
        D3DADAPTER_DEFAULT,
        D3DDEVTYPE_HAL,
        window,
        D3DCREATE_SOFTWARE_VERTEXPROCESSING,
        &present,
        &device);
    if (FAILED(result) || !device)
    {
        fprintf(stderr, "d3d9_device_create_failed=0x%08lx\n", (unsigned long)result);
        IDirect3D9_Release(direct3d);
        DestroyWindow(window);
        return 21;
    }

    printf("AETHERXIV_DXVK_PROBE_OK\n");
    IDirect3DDevice9_Release(device);
    IDirect3D9_Release(direct3d);
    DestroyWindow(window);
    return 0;
}
