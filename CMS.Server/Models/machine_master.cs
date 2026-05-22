namespace CMS.Server.Models
{
    public class machine_master
    {
        public int id_machine { get; set; }
        public string? machine_name { get; set; }
        public string? packer { get; set; }
        public string? material { get; set; }
        public int id_type { get; set; }
        public int mould { get; set; }
        public string? type { get; set; }
        public string? jo_no { get; set; }
        public int qty_order { get; set; }
        public int wip_opening { get; set; }
        public int wip_closing { get; set; }
        public int finish_good { get; set; }
        public int qty_accum { get; set; }
        public int qty_perct { get; set; }
        public float sap_ct { get; set; }
        public float act_ct { get; set; }
        public float part_weight { get; set; }
        public float gross_weight { get; set; }
        public bool status_start { get; set; }
        public bool status_off { get; set; }
        public bool production_running { get; set; }
        public int shot_accum { get; set; }
        public int shot { get; set; }
        public int shift { get; set; }
        public int visual_qc { get; set; }
        public int measure_qc { get; set; }
        public float reject_panelling { get; set; }
        public float reject_lumpy { get; set; }
        public float reject_black_dot { get; set; }
        public float reject_burst { get; set; }
        public float reject_startup { get; set; }
        public float reject_preform { get; set; }
        public float reject_purging { get; set; }
        public float reject_others { get; set; }
        public bool reject_signal { get; set; }
        public DateTime time { get; set; }
        public string? stop_category { get; set; }
        public int mould_category_no { get; set; }
        public string? remark { get; set; }
        public bool remark_signal { get; set; }
        public bool util_barrel { get; set; }
        public bool util_hyd_motor { get; set; }
        public bool util_dehumidifier { get; set; }
        public bool util_chiller { get; set; }
        public bool util_material { get; set; }
        public bool util_dry_cycle { get; set; }
    }
}
