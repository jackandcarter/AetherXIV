/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * AetherXIV is free software: you may redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Runtime.InteropServices;
using System.Text;
using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

internal static class UmbraNativeUi
{
    private const string BootstrapLibrary = "Aether.Umbra.Bootstrap.x86.dll";

    internal static string GetMapObservation()
    {
        var json = new StringBuilder(8192);
        try { return GetMapObservationNative(json, json.Capacity) != 0 ? json.ToString() : "{\"available\":false}"; }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        { return "{\"available\":false,\"reason\":\"Native map observation is unavailable in this build.\"}"; }
    }

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraGetMapObservation", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int GetMapObservationNative(StringBuilder output, int capacity);

    internal static bool BeginWindow(string title, ref int isOpen) =>
        BeginWindowNative(title, ref isOpen) != 0;

    internal static void EndWindow() => EndWindowNative();

    internal static void Text(string text) => TextNative(text);

    internal static void Text(string text, UmbraTextTone tone) => TextToneNative((int)tone, text);

    internal static void Text(string text, UmbraTextTone tone, UmbraTextStyle style) =>
        TextStyledNative((int)tone, (int)style, text);

    internal static bool InputText(string label, ref string value, string hint, int maximumLength)
    {
        int capacity = Math.Clamp(maximumLength, 2, 4096);
        byte[] buffer = new byte[capacity];
        byte[] encoded = Encoding.UTF8.GetBytes(value ?? "");
        int byteCount = Math.Min(encoded.Length, capacity - 1);
        while (byteCount > 0 && byteCount < encoded.Length && (encoded[byteCount] & 0xc0) == 0x80)
            byteCount--;
        Array.Copy(encoded, buffer, byteCount);

        bool changed = InputTextNative(label, hint ?? "", buffer, buffer.Length) != 0;
        int terminator = Array.IndexOf(buffer, (byte)0);
        value = Encoding.UTF8.GetString(buffer, 0, terminator < 0 ? buffer.Length : terminator);
        return changed;
    }

    internal static bool Button(string label) => ButtonNative(label) != 0;

    internal static bool Button(
        string label,
        UmbraButtonStyle style,
        UmbraIcon icon,
        float width,
        float height) =>
        ButtonStyledNative(label, (int)style, (int)icon, width, height) != 0;

    internal static bool Checkbox(string label, ref int value) =>
        CheckboxNative(label, ref value) != 0;

    internal static bool Toggle(string label, ref int value) =>
        ToggleNative(label, ref value) != 0;

    internal static bool InputInt(string label, ref int value, int step) =>
        InputIntNative(label, ref value, step) != 0;

    internal static bool SliderInt(string label, ref int value, int minimum, int maximum) =>
        SliderIntNative(label, ref value, minimum, maximum) != 0;

    internal static bool SliderFloat(string label, ref float value, float minimum, float maximum) =>
        SliderFloatNative(label, ref value, minimum, maximum) != 0;

    internal static bool Combo(string label, ref int selectedIndex, IReadOnlyList<string> items)
    {
        using MemoryStream buffer = new();
        foreach (string item in items)
        {
            byte[] encoded = Encoding.UTF8.GetBytes(item);
            buffer.Write(encoded);
            buffer.WriteByte(0);
        }
        buffer.WriteByte(0);
        return ComboNative(label, ref selectedIndex, buffer.ToArray()) != 0;
    }

    internal static bool CollapsingHeader(string label, bool defaultOpen) =>
        CollapsingHeaderNative(label, defaultOpen ? 1 : 0) != 0;

    internal static void ProgressBar(float fraction, string overlay) =>
        ProgressBarNative(fraction, overlay ?? "");

    internal static void SameLine() => SameLineNative();

    internal static void Separator() => SeparatorNative();

    internal static void Spacing(float height) => SpacingNative(height);

    internal static void Icon(UmbraIcon icon, UmbraTextTone tone, float size) =>
        IconNative((int)icon, (int)tone, size);

    internal static void Badge(string text, UmbraTextTone tone, UmbraIcon icon) =>
        BadgeNative(text, (int)tone, (int)icon);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiImage", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    internal static extern int Image(string path, float size);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiPostNotification", CallingConvention = CallingConvention.StdCall)]
    internal static extern void PostNotification([MarshalAs(UnmanagedType.LPUTF8Str)] string message, int tone);

    internal static void Artwork(string seed, UmbraIcon icon, float size) =>
        ArtworkNative(seed, (int)icon, size);

    internal static void SetNextWindowSize(float width, float height, bool firstUseOnly) =>
        SetNextWindowSizeNative(width, height, firstUseOnly ? 1 : 0);

    internal static float GetAvailableContentWidth() => GetAvailableContentWidthNative();

    internal static float GetContentRegionWidth() => GetContentRegionWidthNative();

    internal static bool BeginChild(string id, float height, bool border) =>
        BeginChildNative(id, height, border ? 1 : 0) != 0;

    internal static void EndChild() => EndChildNative();

    internal static bool BeginPanel(
        string id,
        float width,
        float height,
        UmbraPanelStyle style) =>
        BeginPanelNative(id, width, height, (int)style) != 0;

    internal static void SetPluginManagerOpen(bool isOpen) =>
        SetPluginManagerOpenNative(isOpen ? 1 : 0);

    internal static void SetPluginUpdateCount(int updateCount) =>
        SetPluginUpdateCountNative(Math.Max(0, updateCount));

    internal static void DrawSettingsContent() => DrawSettingsContentNative();

    internal static void SetManagedDevBridgeOwnership(bool managedOwnsBridge) =>
        SetManagedDevBridgeOwnershipNative(managedOwnsBridge ? 1 : 0);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiBeginWindow", CallingConvention = CallingConvention.StdCall)]
    private static extern int BeginWindowNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
        ref int isOpen);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiEndWindow", CallingConvention = CallingConvention.StdCall)]
    private static extern void EndWindowNative();

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiText", CallingConvention = CallingConvention.StdCall)]
    private static extern void TextNative([MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiTextTone", CallingConvention = CallingConvention.StdCall)]
    private static extern void TextToneNative(
        int tone,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiTextStyled", CallingConvention = CallingConvention.StdCall)]
    private static extern void TextStyledNative(
        int tone,
        int style,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiInputText", CallingConvention = CallingConvention.StdCall)]
    private static extern int InputTextNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string hint,
        [In, Out] byte[] buffer,
        int capacity);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiButton", CallingConvention = CallingConvention.StdCall)]
    private static extern int ButtonNative([MarshalAs(UnmanagedType.LPUTF8Str)] string label);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiButtonStyled", CallingConvention = CallingConvention.StdCall)]
    private static extern int ButtonStyledNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        int style,
        int icon,
        float width,
        float height);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiCheckbox", CallingConvention = CallingConvention.StdCall)]
    private static extern int CheckboxNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        ref int value);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiToggle", CallingConvention = CallingConvention.StdCall)]
    private static extern int ToggleNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        ref int value);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiInputInt", CallingConvention = CallingConvention.StdCall)]
    private static extern int InputIntNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        ref int value,
        int step);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiSliderInt", CallingConvention = CallingConvention.StdCall)]
    private static extern int SliderIntNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        ref int value,
        int minimum,
        int maximum);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiSliderFloat", CallingConvention = CallingConvention.StdCall)]
    private static extern int SliderFloatNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        ref float value,
        float minimum,
        float maximum);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiCombo", CallingConvention = CallingConvention.StdCall)]
    private static extern int ComboNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        ref int selectedIndex,
        [In] byte[] items);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiCollapsingHeader", CallingConvention = CallingConvention.StdCall)]
    private static extern int CollapsingHeaderNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        int defaultOpen);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiProgressBar", CallingConvention = CallingConvention.StdCall)]
    private static extern void ProgressBarNative(
        float fraction,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string overlay);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiSameLine", CallingConvention = CallingConvention.StdCall)]
    private static extern void SameLineNative();

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiSeparator", CallingConvention = CallingConvention.StdCall)]
    private static extern void SeparatorNative();

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiSpacing", CallingConvention = CallingConvention.StdCall)]
    private static extern void SpacingNative(float height);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiIcon", CallingConvention = CallingConvention.StdCall)]
    private static extern void IconNative(int icon, int tone, float size);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiBadge", CallingConvention = CallingConvention.StdCall)]
    private static extern void BadgeNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        int tone,
        int icon);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiArtwork", CallingConvention = CallingConvention.StdCall)]
    private static extern void ArtworkNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string seed,
        int icon,
        float size);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiSetNextWindowSize", CallingConvention = CallingConvention.StdCall)]
    private static extern void SetNextWindowSizeNative(float width, float height, int firstUseOnly);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiGetAvailableContentWidth", CallingConvention = CallingConvention.StdCall)]
    private static extern float GetAvailableContentWidthNative();

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiGetContentRegionWidth", CallingConvention = CallingConvention.StdCall)]
    private static extern float GetContentRegionWidthNative();

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiBeginChild", CallingConvention = CallingConvention.StdCall)]
    private static extern int BeginChildNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string id,
        float height,
        int border);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiEndChild", CallingConvention = CallingConvention.StdCall)]
    private static extern void EndChildNative();

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiBeginPanel", CallingConvention = CallingConvention.StdCall)]
    private static extern int BeginPanelNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string id,
        float width,
        float height,
        int style);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiSetPluginManagerOpen", CallingConvention = CallingConvention.StdCall)]
    private static extern void SetPluginManagerOpenNative(int isOpen);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiSetPluginUpdateCount", CallingConvention = CallingConvention.StdCall)]
    private static extern void SetPluginUpdateCountNative(int updateCount);

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraUiDrawSettingsContent", CallingConvention = CallingConvention.StdCall)]
    private static extern void DrawSettingsContentNative();

    [DllImport(BootstrapLibrary, EntryPoint = "UmbraDevBridgeSetManagedOwnership", CallingConvention = CallingConvention.StdCall)]
    private static extern void SetManagedDevBridgeOwnershipNative(int managedOwnsBridge);
}
