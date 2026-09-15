/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

namespace AetherXIV.Launcher.Core;

public sealed record RuntimePreparationResult(
    bool IsReady,
    bool ValidationWasCached,
    bool ConfigurationWasCached,
    string Message,
    RuntimeValidationResult? Validation,
    WineRuntimeConfigurationResult? Configuration,
    RuntimeReadinessAssessment Assessment);

public static class RuntimePreparationService
{
    public static async Task<RuntimePreparationResult> PrepareAsync(
        RuntimeReadinessContext context,
        bool forceValidation = false,
        bool configure = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        RuntimeReadinessAssessment assessment = RuntimeReadinessStore.Assess(context);
        RuntimeValidationResult? validation = null;
        bool validationWasCached = !forceValidation && assessment.ValidationIsCurrent;
        if (!validationWasCached)
        {
            validation = await RuntimeValidator.ValidateAsync(
                context.RuntimeProfile,
                context.PrefixPath,
                context.UmbraInstall,
                context.RuntimeInstall,
                verifyBundledIntegrity: true,
                cancellationToken);
            if (!validation.IsReady)
            {
                RuntimeReadinessStore.Invalidate(context.ReceiptPath);
                return new RuntimePreparationResult(
                    false,
                    false,
                    false,
                    validation.Message,
                    validation,
                    null,
                    assessment);
            }

            RuntimeReadinessStore.RecordValidation(context);
            assessment = RuntimeReadinessStore.Assess(context);
        }

        if (!configure)
        {
            return new RuntimePreparationResult(
                true,
                validationWasCached,
                false,
                validationWasCached ? assessment.Reason : validation!.Message,
                validation,
                null,
                assessment);
        }

        WineRuntimeConfigurationResult? configuration = null;
        bool configurationWasCached = assessment.ConfigurationIsCurrent;
        if (!configurationWasCached)
        {
            configuration = await WineRuntimeConfigurator.ConfigureAsync(
                context.RuntimeProfile,
                context.PrefixPath,
                context.ConfigurationSettings,
                cancellationToken);
            if (!configuration.IsReady)
            {
                return new RuntimePreparationResult(
                    false,
                    validationWasCached,
                    false,
                    configuration.Message,
                    validation,
                    configuration,
                    assessment);
            }

            RuntimeReadinessStore.RecordConfiguration(context);
            assessment = RuntimeReadinessStore.Assess(context);
        }

        string message = validationWasCached && configurationWasCached
            ? assessment.Reason
            : "The bundled runtime, prefix, helper, Umbra payload, and Wine configuration are ready.";
        return new RuntimePreparationResult(
            true,
            validationWasCached,
            configurationWasCached,
            message,
            validation,
            configuration,
            assessment);
    }
}
