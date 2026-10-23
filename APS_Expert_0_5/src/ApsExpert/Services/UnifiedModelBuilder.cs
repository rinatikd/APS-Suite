using ApsExpert.Domain;
using ApsExpert.Geometry;
using ApsExpert.Reference;

namespace ApsExpert.Services;

public sealed class UnifiedModelBuilder
{
    public EngineeringModel Build(GeometrySnapshot geometry, RubezhCadMappingResult? mapping = null)
    {
        var model = new EngineeringModel { DrawingName = geometry.DrawingName };
        var roomByCadId = new Dictionary<Autodesk.AutoCAD.DatabaseServices.ObjectId, Room>();

        foreach (var r in geometry.Rooms)
        {
            var room = new Room
            {
                Number = r.ObjectId.Handle.ToString(),
                Name = r.Layer,
                AreaM2 = r.AreaM2,
                Centroid = r.Centroid
            };
            room.Boundary.AddRange(r.Boundary);
            model.Rooms.Add(room);
            roomByCadId[r.ObjectId] = room;
        }

        foreach (var d in geometry.Detectors)
        {
            var detector = new FireDetector
            {
                CadObjectId = d.ObjectId,
                Position = d.Position,
                Tag = d.Tag,
                Type = d.Type,
                RoomId = d.RoomObjectId.HasValue && roomByCadId.TryGetValue(d.RoomObjectId.Value, out var room) ? room.Id : null
            };
            if (mapping != null)
            {
                var mapped = mapping.Mapped.FirstOrDefault(x => x.CadObjectId == d.ObjectId);
                if (mapped != null)
                {
                    detector.RubezhUid = mapped.Equipment.Uid;
                    detector.RubezhHandle = mapped.Equipment.Handle;
                    detector.RubezhArticle = mapped.Equipment.Article;
                    detector.RubezhLines = mapped.Equipment.Lines;
                    if (!string.IsNullOrWhiteSpace(mapped.Equipment.PositionName)) detector.Tag = mapped.Equipment.PositionName;
                }
            }
            model.Detectors.Add(detector);
        }
        return model;
    }
}
