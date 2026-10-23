using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ApsExpert.Domain;

namespace ApsExpert.Services;

public sealed class CadModelExtractor
{
    public EngineeringModel Extract()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var db = doc.Database;
        var model = new EngineeringModel { DrawingName = doc.Name };

        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId id in ms)
        {
            if (!id.ObjectClass.IsDerivedFrom(RXClass.GetClass(typeof(BlockReference))))
                continue;

            var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
            if (br == null) continue;

            var name = GetEffectiveBlockName(br, tr);
            var upper = name.ToUpperInvariant();

            if (LooksLikeDetector(upper))
            {
                model.Detectors.Add(new FireDetector
                {
                    CadObjectId = id,
                    Position = br.Position,
                    Tag = name
                });
            }
            else if (LooksLikePanel(upper))
            {
                model.Panels.Add(new FireAlarmPanel { CadObjectId = id, Tag = name });
            }
        }

        tr.Commit();
        return model;
    }

    private static string GetEffectiveBlockName(BlockReference br, Transaction tr)
    {
        var btr = (BlockTableRecord)tr.GetObject(br.DynamicBlockTableRecord != ObjectId.Null
            ? br.DynamicBlockTableRecord
            : br.BlockTableRecord, OpenMode.ForRead);
        return btr.Name;
    }

    private static bool LooksLikeDetector(string n)
        => n.Contains("ИП") || n.Contains("ДЫМ") || n.Contains("ДАТЧ") || n.Contains("SMOKE") || n.Contains("DETECT");

    private static bool LooksLikePanel(string n)
        => n.Contains("ППКП") || n.Contains("ППК") || n.Contains("ПРИБОР") || n.Contains("PANEL");
}
