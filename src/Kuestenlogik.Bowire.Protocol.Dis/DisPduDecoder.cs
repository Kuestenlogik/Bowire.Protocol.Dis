// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Frozen;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis;

/// <summary>
/// One place that turns a PDU's bytes into its typed record, by the type byte
/// in its header (#22, #23).
/// </summary>
/// <remarks>
/// <para>
/// Every PDU family already had a typed <c>Unmarshal</c>, round-trip tested,
/// and nothing on the stream path used any of them except EntityState: every
/// other PDU reached the workbench as a header and a base64 blob, and on an
/// entity-scoped subscription it was dropped outright, because nothing could
/// tell which entity it was about. This table is what lets the stream do both.
/// </para>
/// <para>
/// The table is generated from the records themselves — each one names its
/// own type where it marshals — and a test holds it to that: every record
/// type with an <c>Unmarshal</c> appears here, under the type it writes.
/// </para>
/// </remarks>
internal static class DisPduDecoder
{
    internal static readonly FrozenDictionary<DisPduType, Func<byte[], object>> Decoders =
        new Dictionary<DisPduType, Func<byte[], object>>
    {
        [DisPduType.Acknowledge] = b => Pdu.SimulationManagement.AcknowledgePdu.Unmarshal(b),
        [DisPduType.AcknowledgeR] = b => Pdu.SimulationManagementReliability.AcknowledgeRPdu.Unmarshal(b),
        [DisPduType.ActionRequest] = b => Pdu.SimulationManagement.ActionRequestPdu.Unmarshal(b),
        [DisPduType.ActionRequestR] = b => Pdu.SimulationManagementReliability.ActionRequestRPdu.Unmarshal(b),
        [DisPduType.ActionResponse] = b => Pdu.SimulationManagement.ActionResponsePdu.Unmarshal(b),
        [DisPduType.ActionResponseR] = b => Pdu.SimulationManagementReliability.ActionResponseRPdu.Unmarshal(b),
        [DisPduType.AggregateState] = b => Pdu.EntityManagement.AggregateStatePdu.Unmarshal(b),
        [DisPduType.Appearance] = b => Pdu.LiveEntity.AppearancePdu.Unmarshal(b),
        [DisPduType.ArealObjectState] = b => Pdu.SyntheticEnvironment.ArealObjectStatePdu.Unmarshal(b),
        [DisPduType.ArticulatedParts] = b => Pdu.LiveEntity.ArticulatedPartsPdu.Unmarshal(b),
        [DisPduType.Attribute] = b => Pdu.AttributePdu.Unmarshal(b),
        [DisPduType.Collision] = b => Pdu.CollisionPdu.Unmarshal(b),
        [DisPduType.CollisionElastic] = b => Pdu.CollisionElasticPdu.Unmarshal(b),
        [DisPduType.Comment] = b => Pdu.SimulationManagement.CommentPdu.Unmarshal(b),
        [DisPduType.CommentR] = b => Pdu.SimulationManagementReliability.CommentRPdu.Unmarshal(b),
        [DisPduType.CreateEntity] = b => Pdu.SimulationManagement.CreateEntityPdu.Unmarshal(b),
        [DisPduType.CreateEntityR] = b => Pdu.SimulationManagementReliability.CreateEntityRPdu.Unmarshal(b),
        [DisPduType.Data] = b => Pdu.SimulationManagement.DataPdu.Unmarshal(b),
        [DisPduType.DataQuery] = b => Pdu.SimulationManagement.DataQueryPdu.Unmarshal(b),
        [DisPduType.DataQueryR] = b => Pdu.SimulationManagementReliability.DataQueryRPdu.Unmarshal(b),
        [DisPduType.DataR] = b => Pdu.SimulationManagementReliability.DataRPdu.Unmarshal(b),
        [DisPduType.Designator] = b => Pdu.Emissions.DesignatorPdu.Unmarshal(b),
        [DisPduType.Detonation] = b => Pdu.DetonationPdu.Unmarshal(b),
        [DisPduType.DirectedEnergyFire] = b => Pdu.DirectedEnergyFirePdu.Unmarshal(b),
        [DisPduType.ElectromagneticEmission] = b => Pdu.Emissions.ElectromagneticEmissionPdu.Unmarshal(b),
        [DisPduType.EntityDamageStatus] = b => Pdu.EntityDamageStatusPdu.Unmarshal(b),
        [DisPduType.EntityState] = b => Pdu.EntityStatePdu.Unmarshal(b),
        [DisPduType.EntityStateUpdate] = b => Pdu.EntityStateUpdatePdu.Unmarshal(b),
        [DisPduType.EnvironmentalProcess] = b => Pdu.SyntheticEnvironment.EnvironmentalProcessPdu.Unmarshal(b),
        [DisPduType.EventReport] = b => Pdu.SimulationManagement.EventReportPdu.Unmarshal(b),
        [DisPduType.EventReportR] = b => Pdu.SimulationManagementReliability.EventReportRPdu.Unmarshal(b),
        [DisPduType.Fire] = b => Pdu.FirePdu.Unmarshal(b),
        [DisPduType.GriddedData] = b => Pdu.SyntheticEnvironment.GriddedDataPdu.Unmarshal(b),
        [DisPduType.InformationOperationsAction] = b => Pdu.InformationOperations.InformationOperationsActionPdu.Unmarshal(b),
        [DisPduType.InformationOperationsReport] = b => Pdu.InformationOperations.InformationOperationsReportPdu.Unmarshal(b),
        [DisPduType.IntercomControl] = b => Pdu.RadioCommunications.IntercomControlPdu.Unmarshal(b),
        [DisPduType.IntercomSignal] = b => Pdu.RadioCommunications.IntercomSignalPdu.Unmarshal(b),
        [DisPduType.IsGroupOf] = b => Pdu.EntityManagement.IsGroupOfPdu.Unmarshal(b),
        [DisPduType.IsPartOf] = b => Pdu.EntityManagement.IsPartOfPdu.Unmarshal(b),
        [DisPduType.LinearObjectState] = b => Pdu.SyntheticEnvironment.LinearObjectStatePdu.Unmarshal(b),
        [DisPduType.LiveEntityDetonation] = b => Pdu.LiveEntity.LiveEntityDetonationPdu.Unmarshal(b),
        [DisPduType.LiveEntityFire] = b => Pdu.LiveEntity.LiveEntityFirePdu.Unmarshal(b),
        [DisPduType.MinefieldData] = b => Pdu.Minefield.MinefieldDataPdu.Unmarshal(b),
        [DisPduType.MinefieldQuery] = b => Pdu.Minefield.MinefieldQueryPdu.Unmarshal(b),
        [DisPduType.MinefieldResponseNack] = b => Pdu.Minefield.MinefieldResponseNackPdu.Unmarshal(b),
        [DisPduType.MinefieldState] = b => Pdu.Minefield.MinefieldStatePdu.Unmarshal(b),
        [DisPduType.PointObjectState] = b => Pdu.SyntheticEnvironment.PointObjectStatePdu.Unmarshal(b),
        [DisPduType.Receiver] = b => Pdu.RadioCommunications.ReceiverPdu.Unmarshal(b),
        [DisPduType.RecordQueryR] = b => Pdu.SimulationManagementReliability.RecordQueryRPdu.Unmarshal(b),
        [DisPduType.RecordR] = b => Pdu.SimulationManagementReliability.RecordRPdu.Unmarshal(b),
        [DisPduType.RemoveEntity] = b => Pdu.SimulationManagement.RemoveEntityPdu.Unmarshal(b),
        [DisPduType.RemoveEntityR] = b => Pdu.SimulationManagementReliability.RemoveEntityRPdu.Unmarshal(b),
        [DisPduType.RepairComplete] = b => Pdu.Logistics.RepairCompletePdu.Unmarshal(b),
        [DisPduType.RepairResponse] = b => Pdu.Logistics.RepairResponsePdu.Unmarshal(b),
        [DisPduType.ResupplyCancel] = b => Pdu.Logistics.ResupplyCancelPdu.Unmarshal(b),
        [DisPduType.ResupplyOffer] = b => Pdu.Logistics.ResupplyOfferPdu.Unmarshal(b),
        [DisPduType.ResupplyReceived] = b => Pdu.Logistics.ResupplyReceivedPdu.Unmarshal(b),
        [DisPduType.ServiceRequest] = b => Pdu.Logistics.ServiceRequestPdu.Unmarshal(b),
        [DisPduType.SetData] = b => Pdu.SimulationManagement.SetDataPdu.Unmarshal(b),
        [DisPduType.SetDataR] = b => Pdu.SimulationManagementReliability.SetDataRPdu.Unmarshal(b),
        [DisPduType.SetRecordR] = b => Pdu.SimulationManagementReliability.SetRecordRPdu.Unmarshal(b),
        [DisPduType.Signal] = b => Pdu.RadioCommunications.SignalPdu.Unmarshal(b),
        [DisPduType.StartResume] = b => Pdu.SimulationManagement.StartResumePdu.Unmarshal(b),
        [DisPduType.StartResumeR] = b => Pdu.SimulationManagementReliability.StartResumeRPdu.Unmarshal(b),
        [DisPduType.StopFreeze] = b => Pdu.SimulationManagement.StopFreezePdu.Unmarshal(b),
        [DisPduType.StopFreezeR] = b => Pdu.SimulationManagementReliability.StopFreezeRPdu.Unmarshal(b),
        [DisPduType.SupplementalEmissionEntityState] = b => Pdu.Emissions.SupplementalEmissionEntityStatePdu.Unmarshal(b),
        [DisPduType.TimeSpacePositionInformation] = b => Pdu.LiveEntity.TimeSpacePositionInformationPdu.Unmarshal(b),
        [DisPduType.TransferOwnership] = b => Pdu.EntityManagement.TransferOwnershipPdu.Unmarshal(b),
        [DisPduType.Transmitter] = b => Pdu.RadioCommunications.TransmitterPdu.Unmarshal(b),
        [DisPduType.UnderwaterAcoustic] = b => Pdu.Emissions.UnderwaterAcousticPdu.Unmarshal(b),
    }.ToFrozenDictionary();

    /// <summary>
    /// The typed record for <paramref name="buffer"/>, or null when the type is
    /// not one Bowire decodes or the bytes do not hold a well-formed PDU of it.
    /// A malformed PDU on a live exercise network is ordinary; it must never
    /// take the stream down.
    /// </summary>
    internal static object? TryDecode(byte[] buffer)
    {
        if (buffer.Length < PduHeader.WireLength) return null;
        if (!Decoders.TryGetValue((DisPduType)buffer[2], out var decode)) return null;
        try { return decode(buffer); }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException
            or InvalidOperationException or FormatException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// Every entity a decoded PDU names — firing and target entity of a Fire,
    /// both sides of a Collision, the designated entity of a Designator, the
    /// members of an aggregate. Read off the record's <see cref="EntityId"/>
    /// properties (and lists of them), so a PDU family added later is covered
    /// without a second table to keep in step. The all-zero id means "no
    /// entity" in DIS and is left out.
    /// </summary>
    internal static IReadOnlyList<EntityId> RelatedEntities(object pdu)
    {
        ArgumentNullException.ThrowIfNull(pdu);
        var ids = new List<EntityId>();
        foreach (var property in pdu.GetType().GetProperties())
        {
            if (property.GetIndexParameters().Length > 0) continue;
            var value = property.GetValue(pdu);
            switch (value)
            {
                case EntityId id:
                    Add(id);
                    break;
                case IEnumerable list and not string:
                    foreach (var item in list)
                        if (item is EntityId inner) Add(inner);
                    break;
            }
        }
        return ids;

        void Add(EntityId id)
        {
            if (id.Site == 0 && id.Application == 0 && id.Entity == 0) return;
            if (!ids.Contains(id)) ids.Add(id);
        }
    }
}
