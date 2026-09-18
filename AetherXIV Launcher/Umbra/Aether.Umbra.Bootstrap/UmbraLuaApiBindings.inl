// Verified 1.23b Lua API entry points; all signatures must match before patching.
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x08, 0x57, 0x8b, 0x7c, 0x24};
    if (memcmp(base + 0x9ced90, expected, sizeof(expected)) != 0) return false;
    api.getupvalue = reinterpret_cast<decltype(api.getupvalue)>(base + 0x9ced90);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x08, 0x57, 0x8b, 0x7c, 0x24};
    if (memcmp(base + 0x9cee20, expected, sizeof(expected)) != 0) return false;
    api.setupvalue = reinterpret_cast<decltype(api.setupvalue)>(base + 0x9cee20);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x04, 0x50, 0xe8, 0x96, 0x32};
    if (memcmp(base + 0x9cebc0, expected, sizeof(expected)) != 0) return false;
    api.error = reinterpret_cast<decltype(api.error)>(base + 0x9cebc0);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x04, 0x8b, 0x48, 0x08, 0x8b};
    if (memcmp(base + 0x9ce380, expected, sizeof(expected)) != 0) return false;
    api.pushlightuserdata = reinterpret_cast<decltype(api.pushlightuserdata)>(base + 0x9ce380);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x08, 0x8b, 0x4c, 0x24, 0x04};
    if (memcmp(base + 0x9ce090, expected, sizeof(expected)) != 0) return false;
    api.touserdata = reinterpret_cast<decltype(api.touserdata)>(base + 0x9ce090);
}
{
    const BYTE expected[] = {0x8b, 0x4c, 0x24, 0x04, 0x8b, 0x41, 0x08, 0x2b};
    if (memcmp(base + 0x9cdaf0, expected, sizeof(expected)) != 0) return false;
    api.gettop = reinterpret_cast<decltype(api.gettop)>(base + 0x9cdaf0);
}
{
    const BYTE expected[] = {0x8b, 0x4c, 0x24, 0x08, 0x85, 0xc9, 0x8b, 0x44};
    if (memcmp(base + 0x9cdb00, expected, sizeof(expected)) != 0) return false;
    api.settop = reinterpret_cast<decltype(api.settop)>(base + 0x9cdb00);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x08, 0x3d, 0x40, 0x1f, 0x00};
    if (memcmp(base + 0x9cd9d0, expected, sizeof(expected)) != 0) return false;
    api.checkstack = reinterpret_cast<decltype(api.checkstack)>(base + 0x9cd9d0);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x08, 0x56, 0x8b, 0x74, 0x24};
    if (memcmp(base + 0x9cdcb0, expected, sizeof(expected)) != 0) return false;
    api.pushvalue = reinterpret_cast<decltype(api.pushvalue)>(base + 0x9cdcb0);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x08, 0x8b, 0x4c, 0x24, 0x04};
    if (memcmp(base + 0x9cdce0, expected, sizeof(expected)) != 0) return false;
    api.type = reinterpret_cast<decltype(api.type)>(base + 0x9cdce0);
}
{
    const BYTE expected[] = {0x56, 0x8b, 0x74, 0x24, 0x0c, 0x57, 0x8b, 0x7c};
    if (memcmp(base + 0x9ce0e0, expected, sizeof(expected)) != 0) return false;
    api.topointer = reinterpret_cast<decltype(api.topointer)>(base + 0x9ce0e0);
}
{
    const BYTE expected[] = {0x56, 0x8b, 0x74, 0x24, 0x08, 0x57, 0x8b, 0x7c};
    if (memcmp(base + 0x9cdf80, expected, sizeof(expected)) != 0) return false;
    api.tolstring = reinterpret_cast<decltype(api.tolstring)>(base + 0x9cdf80);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x08, 0x8b, 0x4c, 0x24, 0x04};
    if (memcmp(base + 0x9cded0, expected, sizeof(expected)) != 0) return false;
    api.tonumber = reinterpret_cast<decltype(api.tonumber)>(base + 0x9cded0);
}
{
    const BYTE expected[] = {0x55, 0x8b, 0x6c, 0x24, 0x0c, 0x85, 0xed, 0x75};
    if (memcmp(base + 0x9ce1f0, expected, sizeof(expected)) != 0) return false;
    api.pushstring = reinterpret_cast<decltype(api.pushstring)>(base + 0x9ce1f0);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x04, 0xdd, 0x44, 0x24, 0x08};
    if (memcmp(base + 0x9ce170, expected, sizeof(expected)) != 0) return false;
    api.pushnumber = reinterpret_cast<decltype(api.pushnumber)>(base + 0x9ce170);
}
{
    const BYTE expected[] = {0x56, 0x8b, 0x74, 0x24, 0x08, 0x8b, 0x46, 0x10};
    if (memcmp(base + 0x9ce2c0, expected, sizeof(expected)) != 0) return false;
    api.pushcclosure = reinterpret_cast<decltype(api.pushcclosure)>(base + 0x9ce2c0);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x08, 0x83, 0xec, 0x10, 0x53};
    if (memcmp(base + 0x9ce400, expected, sizeof(expected)) != 0) return false;
    api.getfield = reinterpret_cast<decltype(api.getfield)>(base + 0x9ce400);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x08, 0x83, 0xec, 0x10, 0x53};
    if (memcmp(base + 0x9ce620, expected, sizeof(expected)) != 0) return false;
    api.setfield = reinterpret_cast<decltype(api.setfield)>(base + 0x9ce620);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x08, 0x56, 0x8b, 0x74, 0x24};
    if (memcmp(base + 0x9ce8a0, expected, sizeof(expected)) != 0) return false;
    api.call = reinterpret_cast<decltype(api.call)>(base + 0x9ce8a0);
}
{
    const BYTE expected[] = {0x8b, 0x44, 0x24, 0x10, 0x83, 0xec, 0x08, 0x85};
    if (memcmp(base + 0x9ce900, expected, sizeof(expected)) != 0) return false;
    api.pcall = reinterpret_cast<decltype(api.pcall)>(base + 0x9ce900);
}
{
    const BYTE expected[] = {0x83, 0xec, 0x08, 0x8b, 0x44, 0x24, 0x10, 0x8b};
    if (memcmp(base + 0x9ce9e0, expected, sizeof(expected)) != 0) return false;
    api.cpcall = reinterpret_cast<decltype(api.cpcall)>(base + 0x9ce9e0);
}
