namespace CMS.Server.Models;

public class Report
{
    #region Identifiers

    public int id_machine { get; set; }
    public int shift { get; set; }
    public required string machine_name { get; set; }
    public string? packer { get; set; }
    public required string material { get; set; }
    public int id_type { get; set; }
    public int mould { get; set; }
    public required string type { get; set; }
    public string? jo_no { get; set; }

    #endregion

    #region Quantities

    public int qty_perct { get; set; }
    public double gross_weight { get; set; }
    public double part_weight { get; set; }
    public int shot_accum { get; set; }
    public int qty_order { get; set; }
    public int wip_opening { get; set; }
    public int wip_closing { get; set; }
    public int shift_output { get; set; }
    public int finish_good { get; set; }
    public double inward { get; set; }
    public int qty_accum { get; set; }
    public int qty_balance { get; set; }
    public double material_used { get; set; }
    public double runner { get; set; }

    #endregion

    #region Rejects

    public double reject_startup { get; set; }
    public double reject_startup_per { get; set; }
    public double reject_prod { get; set; }
    public double reject_prod_per { get; set; }
    public double reject_purging { get; set; }
    public double reject_preform { get; set; }
    public int reject_total_pcs { get; set; }
    public double part_scrap { get; set; }

    #endregion

    #region CycleAndDowntime

    public double act_ct { get; set; }
    public double sap_ct { get; set; }
    public double production_running { get; set; }
    public double change_full_set { get; set; }
    public double change_half_set { get; set; }
    public double change_parts { get; set; }
    public double maintenance_dt { get; set; }
    public double technician_dt { get; set; }
    public double production_dt { get; set; }
    public double unallocated { get; set; }

    #endregion

    #region Meta

    public string? remark { get; set; }

    #endregion
}