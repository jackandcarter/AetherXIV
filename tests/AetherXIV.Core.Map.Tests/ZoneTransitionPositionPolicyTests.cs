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

namespace AetherXIV.Core.Map.Tests;

public sealed class ZoneTransitionPositionPolicyTests
{
    private const uint UldahZone = 175;

    [Fact]
    public void HallPositionIsRejectedAfterUldahTransitionBegins()
    {
        Assert.False(ZoneTransitionPositionPolicy.IsDestinationConsistent(
            hasExpectedPosition: true,
            currentZoneId: UldahZone,
            expectedZoneId: UldahZone,
            expectedX: -210.0f,
            expectedY: 190.0f,
            expectedZ: 25.0f,
            receivedX: 160.105f,
            receivedY: 0.0f,
            receivedZ: -145.482f));
    }

    [Fact]
    public void CapturedUldahArrivalCompletesPendingTransition()
    {
        Assert.True(ZoneTransitionPositionPolicy.IsDestinationConsistent(
            hasExpectedPosition: true,
            currentZoneId: UldahZone,
            expectedZoneId: UldahZone,
            expectedX: -210.0f,
            expectedY: 190.0f,
            expectedZ: 25.0f,
            receivedX: -210.0f,
            receivedY: 190.0f,
            receivedZ: 25.0f));
    }

    [Fact]
    public void SmallClientArrivalAdjustmentIsAccepted()
    {
        Assert.True(ZoneTransitionPositionPolicy.IsDestinationConsistent(
            hasExpectedPosition: true,
            currentZoneId: UldahZone,
            expectedZoneId: UldahZone,
            expectedX: -210.0f,
            expectedY: 190.0f,
            expectedZ: 25.0f,
            receivedX: -214.0f,
            receivedY: 191.0f,
            receivedZ: 28.0f));
    }

    [Fact]
    public void CapturedGridaniaPrivateAreaAcknowledgementIsAccepted()
    {
        Assert.True(ZoneTransitionPositionPolicy.IsDestinationConsistent(
            hasExpectedPosition: true,
            currentZoneId: 166,
            expectedZoneId: 166,
            expectedX: 362.4087f,
            expectedY: 4.0f,
            expectedZ: -703.8168f,
            receivedX: 354.3533f,
            receivedY: 3.750001f,
            receivedZ: -700.6393f));
    }

    [Fact]
    public void WrongCurrentZoneCannotAcknowledgeTransition()
    {
        Assert.False(ZoneTransitionPositionPolicy.IsDestinationConsistent(
            hasExpectedPosition: true,
            currentZoneId: 170,
            expectedZoneId: UldahZone,
            expectedX: -210.0f,
            expectedY: 190.0f,
            expectedZ: 25.0f,
            receivedX: -210.0f,
            receivedY: 190.0f,
            receivedZ: 25.0f));
    }

    [Fact]
    public void OrdinaryMovementIsUnaffectedWithoutPendingExpectation()
    {
        Assert.True(ZoneTransitionPositionPolicy.IsDestinationConsistent(
            hasExpectedPosition: false,
            currentZoneId: 170,
            expectedZoneId: 0,
            expectedX: 0.0f,
            expectedY: 0.0f,
            expectedZ: 0.0f,
            receivedX: 23.069f,
            receivedY: 0.0f,
            receivedZ: 5.685f));
    }

    [Fact]
    public void PrivateToPublicBoundaryUsesRoomExitReload()
    {
        Assert.Equal(
            ZoneTransitionReloadRecipe.PrivateAreaBoundary,
            ZoneTransitionReloadPolicy.Select(
                155,
                "PrivateAreaMasterPast",
                2,
                155,
                null,
                0));
    }

    [Fact]
    public void RoomExitBootstrapUsesCapturedRetailTimingAndBatchBounds()
    {
        DateTime queuedAt = new(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc);
        DateTime bootstrapAt =
            ZoneTransitionBootstrapPolicy.GetBootstrapDueAt(queuedAt);
        DateTime firstActorBatchAt =
            ZoneTransitionBootstrapPolicy.GetFirstActorBatchDueAt(bootstrapAt);

        Assert.Equal(6000, (bootstrapAt - queuedAt).TotalMilliseconds);
        Assert.Equal(440, (firstActorBatchAt - bootstrapAt).TotalMilliseconds);
        Assert.Equal(140, ZoneTransitionBootstrapPolicy.ActorBatchIntervalMilliseconds);
        Assert.Equal(8, ZoneTransitionBootstrapPolicy.ActorsPerBatch);
        Assert.True(ZoneTransitionBootstrapPolicy.RequiresDeferredBootstrap(
            ZoneTransitionReloadRecipe.PrivateAreaBoundary));
        Assert.False(ZoneTransitionBootstrapPolicy.RequiresDeferredBootstrap(
            ZoneTransitionReloadRecipe.FullMap));
    }

    [Fact]
    public void SamePublicAreaMoveUsesResidentGeometryReload()
    {
        Assert.Equal(
            ZoneTransitionReloadRecipe.ResidentGeometry,
            ZoneTransitionReloadPolicy.Select(155, null, 0, 155, null, 0));
    }

    [Fact]
    public void CrossZoneTutorialReturnUsesFullMapReload()
    {
        Assert.Equal(
            ZoneTransitionReloadRecipe.FullMap,
            ZoneTransitionReloadPolicy.Select(
                166,
                "PrivateAreaMasterPast",
                1,
                155,
                null,
                0));
    }

    [Theory]
    [InlineData(false, -1, false)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, false)]
    [InlineData(true, -1, true)]
    public void DeferredNoticeOnlyReleasesForRetailZoneReadyAcknowledgement(
        bool packetValid,
        int unknown,
        bool expected)
    {
        Assert.Equal(
            expected,
            ZoneTransitionReadinessPolicy.IsReady(packetValid, unknown));
    }
}
