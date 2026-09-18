// Catalog images are downloaded by managed code; this layer only decodes bounded
// local files and owns DX9 textures on the render thread.
namespace PluginImages
{
    IDirect3DDevice9* device = nullptr;
    struct Cached { wchar_t path[1024]{}; IDirect3DTexture9* texture = nullptr; };
    Cached cache[128]{};
    unsigned int count = 0;
    void SetDevice(IDirect3DDevice9* value)
    {
        if (device == value) return;
        for (unsigned int i = 0; i < count; ++i)
            if (cache[i].texture) cache[i].texture->Release();
        count = 0; device = value;
    }
    IDirect3DTexture9* Load(const wchar_t* path)
    {
        WIN32_FILE_ATTRIBUTE_DATA info{};
        if (!GetFileAttributesExW(path, GetFileExInfoStandard, &info)
            || info.nFileSizeHigh || info.nFileSizeLow > 4 * 1024 * 1024) return nullptr;
        HMODULE ole = GetModuleHandleW(L"ole32.dll");
        if (!ole) ole = LoadLibraryW(L"ole32.dll");
        if (!ole) return nullptr;
        using Initialize = HRESULT (WINAPI*)(LPVOID, DWORD);
        using Uninitialize = void (WINAPI*)();
        using Create = HRESULT (WINAPI*)(REFCLSID, LPUNKNOWN, DWORD, REFIID, LPVOID*);
        auto initialize = reinterpret_cast<Initialize>(GetProcAddress(ole, "CoInitializeEx"));
        auto uninitialize = reinterpret_cast<Uninitialize>(GetProcAddress(ole, "CoUninitialize"));
        auto create = reinterpret_cast<Create>(GetProcAddress(ole, "CoCreateInstance"));
        if (!initialize || !uninitialize || !create) return nullptr;
        HRESULT initialized = initialize(nullptr, COINIT_MULTITHREADED);
        if (FAILED(initialized) && initialized != RPC_E_CHANGED_MODE) return nullptr;
        const GUID factoryId = {0xcacaf262,0x9370,0x4615,{0xa1,0x3b,0x9f,0x55,0x39,0xda,0x4c,0x0a}};
        const GUID factoryInterface = {0xec5ec8a9,0xc395,0x4314,{0x9c,0x77,0x54,0xd7,0xa9,0x35,0xff,0x70}};
        const GUID bgra = {0x6fddc324,0x4e03,0x4bfe,{0xb1,0x85,0x3d,0x77,0x76,0x8d,0xc9,0x0f}};
        IWICImagingFactory* factory = nullptr;
        IWICBitmapDecoder* decoder = nullptr;
        IWICBitmapFrameDecode* frame = nullptr;
        IWICFormatConverter* converter = nullptr;
        IDirect3DTexture9* texture = nullptr;
        UINT width = 0, height = 0;
        bool valid = SUCCEEDED(create(factoryId, nullptr, CLSCTX_INPROC_SERVER, factoryInterface, reinterpret_cast<void**>(&factory)))
            && SUCCEEDED(factory->CreateDecoderFromFilename(path, nullptr, GENERIC_READ, WICDecodeMetadataCacheOnDemand, &decoder))
            && SUCCEEDED(decoder->GetFrame(0, &frame))
            && SUCCEEDED(frame->GetSize(&width, &height))
            && width > 0 && height > 0 && width <= 1024 && height <= 1024
            && SUCCEEDED(factory->CreateFormatConverter(&converter))
            && SUCCEEDED(converter->Initialize(frame, bgra, WICBitmapDitherTypeNone, nullptr, 0.0, WICBitmapPaletteTypeCustom))
            && SUCCEEDED(device->CreateTexture(width, height, 1, 0, D3DFMT_A8R8G8B8, D3DPOOL_MANAGED, &texture, nullptr));
        if (valid)
        {
            D3DLOCKED_RECT rectangle{};
            valid = SUCCEEDED(texture->LockRect(0, &rectangle, nullptr, 0));
            if (valid)
            {
                valid = SUCCEEDED(converter->CopyPixels(nullptr, rectangle.Pitch, rectangle.Pitch * height,
                    static_cast<BYTE*>(rectangle.pBits)));
                texture->UnlockRect(0);
            }
        }
        if (!valid && texture) { texture->Release(); texture = nullptr; }
        if (converter) converter->Release();
        if (frame) frame->Release();
        if (decoder) decoder->Release();
        if (factory) factory->Release();
        if (SUCCEEDED(initialized)) uninitialize();
        return texture;
    }
    bool Draw(const wchar_t* path, float size)
    {
        if (!device || !path || !*path || wcslen(path) >= 1024) return false;
        unsigned int index = 0;
        for (; index < count; ++index) if (wcscmp(cache[index].path, path) == 0) break;
        if (index == count)
        {
            if (count == 128) return false;
            wcscpy(cache[count].path, path);
            cache[count++].texture = Load(path);
        }
        if (!cache[index].texture) return false;
        ImGui::Image(reinterpret_cast<ImTextureID>(cache[index].texture), ImVec2(size, size));
        return true;
    }
}
