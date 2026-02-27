namespace CMS.Server.Models
{
    public class Report
    {
        public int IdMachine { get; set; }
        public int Shift { get; set; }
        public required string MachineName { get; set; }
        public string? Packer { get; set; }
        public required string Material { get; set; }
        public int IdType { get; set; }
        public int Mould { get; set; }
        public required string Type { get; set; }
        public string? JoNo { get; set; }
        public int QtyPerct { get; set; }
        public double GrossWeight { get; set; }
        public double PartWeight { get; set; }
        public int ShotAccum { get; set; }
        public int QtyOrder { get; set; }
        public int WipOpening { get; set; }
        public int WipClosing { get; set; }
        public int ShiftOutput { get; set; }
        public int FinishGood { get; set; }
        public double Inward { get; set; }
        public int QtyAccum { get; set; }
        public int QtyBalance { get; set; }
        public double MaterialUsed { get; set; }
        public double Runner { get; set; }
        public double RejectStartup { get; set; }
        public double RejectStartupPer { get; set; }
        public double RejectProd { get; set; }
        public double RejectProdPer { get; set; }
        public double ActCt { get; set; }
        public double ProductionRunning { get; set; }
        public double SapCt { get; set; }
        public double ChangeFullSet { get; set; }
        public double ChangeHalfSet { get; set; }
        public double ChangeParts { get; set; }
        public double MaintenanceDt { get; set; }
        public double TechnicianDt { get; set; }
        public double ProductionDt { get; set; }
        public string? Remark { get; set; }
        public double Unallocated { get; set; }
        public double PartScrap { get; set; }
        public double RejectPurging { get; set; }
        public double RejectPreform { get; set; }
        public int RejectTotalPcs { get; set; }
    }
}
