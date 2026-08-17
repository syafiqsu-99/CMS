<template>
    <div class="d-flex flex-column h-100 pa-3" style="min-height: 0;">
        <div class="d-flex align-center flex-shrink-0 mb-2">
            <v-icon color="primary" class="mr-2">mdi-lan</v-icon>
            <div>
                <div class="text-subtitle-1 font-weight-medium">Network &amp; Schema</div>
                <div class="text-caption text-medium-emphasis">PLC network topology, the SQL Server relationship map,
                    and the column reference for every table.</div>
            </div>
        </div>

        <div class="flex-grow-1 overflow-y-auto border rounded pa-4" style="min-height: 0;">

            <div class="section-label">Network Topology</div>
            <div class="diagram-wrap mb-2">
                <svg viewBox="0 0 760 560" preserveAspectRatio="xMidYMid meet" role="img"
                    aria-label="CMS PLC network topology" class="diagram-svg">
                    <defs>
                        <marker id="net-arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6"
                            orient="auto-start-reverse">
                            <path d="M2 1L8 5L2 9" fill="none" stroke="context-stroke" stroke-width="1.5"
                                stroke-linecap="round" stroke-linejoin="round" />
                        </marker>
                        <clipPath id="clip-db">
                            <rect x="576" y="116" width="52" height="52" rx="8" />
                        </clipPath>
                        <clipPath id="clip-central">
                            <rect x="246" y="206" width="56" height="56" rx="8" />
                        </clipPath>
                        <clipPath id="clip-m1">
                            <rect x="71" y="356" width="44" height="44" rx="8" />
                        </clipPath>
                        <clipPath id="clip-m2">
                            <rect x="241" y="356" width="44" height="44" rx="8" />
                        </clipPath>
                        <clipPath id="clip-mn">
                            <rect x="411" y="356" width="44" height="44" rx="8" />
                        </clipPath>
                        <clipPath id="clip-mt">
                            <rect x="581" y="356" width="44" height="44" rx="8" />
                        </clipPath>
                    </defs>

                    <g class="node">
                        <rect x="255" y="30" width="170" height="52" rx="8" class="box" />
                        <text class="th" x="340" y="50" text-anchor="middle"
                            dominant-baseline="central">CMS.Client</text>
                        <text class="ts" x="340" y="68" text-anchor="middle" dominant-baseline="central">Vue 3 &#183;
                            /api proxy</text>
                    </g>
                    <line x1="340" y1="82" x2="340" y2="112" class="arr" marker-end="url(#net-arrow)" />

                    <g class="node">
                        <rect x="235" y="114" width="210" height="56" rx="8" class="box box-teal" />
                        <text class="th" x="340" y="134" text-anchor="middle"
                            dominant-baseline="central">CMS.Server</text>
                        <text class="ts" x="340" y="152" text-anchor="middle" dominant-baseline="central">.NET 8 Web API
                            &#183; IIS</text>
                    </g>

                    <line x1="445" y1="142" x2="572" y2="142" class="arr" marker-end="url(#net-arrow)" />
                    <g class="node">
                        <rect x="560" y="116" width="110" height="52" rx="8" class="box box-blue" />
                        <image :href="imgDatabase" x="576" y="116" width="52" height="52" clip-path="url(#clip-db)"
                            preserveAspectRatio="xMidYMid slice" />
                        <text class="th" x="632" y="134" text-anchor="middle" dominant-baseline="central">SQL
                            Server</text>
                        <text class="ts" x="632" y="152" text-anchor="middle" dominant-baseline="central">raw
                            ADO.NET</text>
                    </g>

                    <line x1="340" y1="170" x2="340" y2="204" class="arr" marker-end="url(#net-arrow)" />

                    <g class="node">
                        <rect x="230" y="206" width="220" height="56" rx="8" class="box box-coral" />
                        <image :href="imgCentralPlc" x="246" y="206" width="56" height="56"
                            clip-path="url(#clip-central)" preserveAspectRatio="xMidYMid slice" />
                        <text class="th" x="360" y="226" text-anchor="middle" dominant-baseline="central">Central PLC
                            (CJ2M)</text>
                        <text class="ts" x="360" y="244" text-anchor="middle"
                            dominant-baseline="central">172.17.86.80</text>
                    </g>

                    <text class="ts" x="340" y="288" text-anchor="middle">FINS/UDP &#183; &#8804;250 words per
                        read</text>

                    <line x1="340" y1="262" x2="340" y2="326" class="bus" />
                    <line x1="115" y1="326" x2="625" y2="326" class="bus" />
                    <line x1="115" y1="326" x2="115" y2="352" class="arr bus" marker-end="url(#net-arrow)" />
                    <line x1="285" y1="326" x2="285" y2="352" class="arr bus" marker-end="url(#net-arrow)" />
                    <line x1="455" y1="326" x2="455" y2="352" class="arr bus" marker-end="url(#net-arrow)" />
                    <line x1="625" y1="326" x2="625" y2="352" class="arr bus" marker-end="url(#net-arrow)" />

                    <g class="node">
                        <rect x="55" y="356" width="120" height="52" rx="8" class="box box-amber" />
                        <image :href="imgPlc" x="71" y="356" width="44" height="44" clip-path="url(#clip-m1)"
                            preserveAspectRatio="xMidYMid slice" />
                        <text class="th" x="127" y="374" text-anchor="middle" dominant-baseline="central">Machine
                            1</text>
                        <text class="ts" x="127" y="392" text-anchor="middle" dominant-baseline="central">.86.221</text>
                    </g>
                    <g class="node">
                        <rect x="225" y="356" width="120" height="52" rx="8" class="box box-amber" />
                        <image :href="imgPlc" x="241" y="356" width="44" height="44" clip-path="url(#clip-m2)"
                            preserveAspectRatio="xMidYMid slice" />
                        <text class="th" x="297" y="374" text-anchor="middle" dominant-baseline="central">Machine
                            2</text>
                        <text class="ts" x="297" y="392" text-anchor="middle" dominant-baseline="central">.86.222</text>
                    </g>
                    <g class="node">
                        <rect x="395" y="356" width="120" height="52" rx="8" class="box box-amber" />
                        <image :href="imgPlc" x="411" y="356" width="44" height="44" clip-path="url(#clip-mn)"
                            preserveAspectRatio="xMidYMid slice" />
                        <text class="th" x="467" y="374" text-anchor="middle" dominant-baseline="central">Machine
                            n</text>
                        <text class="ts" x="467" y="392" text-anchor="middle"
                            dominant-baseline="central">.86.220+n</text>
                    </g>
                    <g class="node">
                        <rect x="565" y="356" width="120" height="52" rx="8" class="box" />
                        <image :href="imgPlc" x="581" y="356" width="44" height="44" clip-path="url(#clip-mt)"
                            preserveAspectRatio="xMidYMid slice" />
                        <text class="th" x="637" y="374" text-anchor="middle" dominant-baseline="central">Machine
                            Test</text>
                        <text class="ts" x="625" y="392" text-anchor="middle" dominant-baseline="central">.86.220</text>
                    </g>

                    <line x1="115" y1="408" x2="115" y2="444" class="arr" marker-end="url(#net-arrow)" />
                    <line x1="285" y1="408" x2="285" y2="444" class="arr" marker-end="url(#net-arrow)" />
                    <line x1="455" y1="408" x2="455" y2="444" class="arr" marker-end="url(#net-arrow)" />
                    <line x1="625" y1="408" x2="625" y2="444" class="arr" marker-end="url(#net-arrow)" />

                    <g>
                        <rect x="55" y="444" width="120" height="44" rx="8" class="box box-blue" />
                        <text class="ts" x="115" y="466" text-anchor="middle"
                            dominant-baseline="central">machine_log_1</text>
                    </g>
                    <g>
                        <rect x="225" y="444" width="120" height="44" rx="8" class="box box-blue" />
                        <text class="ts" x="285" y="466" text-anchor="middle"
                            dominant-baseline="central">machine_log_2</text>
                    </g>
                    <g>
                        <rect x="395" y="444" width="120" height="44" rx="8" class="box box-blue" />
                        <text class="ts" x="455" y="466" text-anchor="middle"
                            dominant-baseline="central">machine_log_n</text>
                    </g>
                    <g>
                        <rect x="565" y="444" width="120" height="44" rx="8" class="box box-blue" />
                        <text class="ts" x="625" y="466" text-anchor="middle"
                            dominant-baseline="central">machine_log_0</text>
                    </g>

                    <rect x="55" y="510" width="14" height="14" rx="3" class="box box-coral" />
                    <text class="ts" x="76" y="517" dominant-baseline="central">Central PLC (CJ2M)</text>
                    <rect x="290" y="510" width="14" height="14" rx="3" class="box box-amber" />
                    <text class="ts" x="311" y="517" dominant-baseline="central">Machine sub-PLC (CP2E)</text>
                    <rect x="500" y="510" width="14" height="14" rx="3" class="box box-blue" />
                    <text class="ts" x="521" y="517" dominant-baseline="central">Log table</text>
                </svg>
            </div>

            <v-divider class="my-6" />

            <div class="section-label">Schema Relationships (ERD)</div>
            <div class="diagram-wrap erd-wrap mb-2">
                <svg viewBox="0 0 1000 860" preserveAspectRatio="xMidYMid meet" role="img"
                    aria-label="CMS SQL Server entity relationship diagram" class="diagram-svg">
                    <defs>
                        <marker id="erd-one" viewBox="0 0 12 12" refX="6" refY="6" markerWidth="11" markerHeight="11"
                            orient="auto-start-reverse">
                            <path d="M6 1 L6 11" stroke="context-stroke" stroke-width="1.6" fill="none" />
                        </marker>
                        <marker id="erd-many" viewBox="0 0 14 14" refX="11" refY="7" markerWidth="13" markerHeight="13"
                            orient="auto-start-reverse">
                            <path d="M2 2 L12 7 L2 12" stroke="context-stroke" stroke-width="1.6" fill="none"
                                stroke-linejoin="round" />
                        </marker>
                    </defs>

                    <path d="M250 110 L350 110" class="rel" marker-start="url(#erd-one)" marker-end="url(#erd-many)" />
                    <path d="M150 170 L150 250 L350 250" class="rel" marker-start="url(#erd-one)"
                        marker-end="url(#erd-many)" />
                    <path d="M130 170 L130 400 L350 400" class="rel" marker-start="url(#erd-one)"
                        marker-end="url(#erd-many)" />
                    <path d="M110 170 L110 540 L350 540" class="rel" marker-start="url(#erd-one)"
                        marker-end="url(#erd-many)" />
                    <path d="M505 150 L505 220" class="rel" marker-start="url(#erd-one)" marker-end="url(#erd-many)" />
                    <path d="M460 150 L460 370" class="rel" marker-start="url(#erd-one)" marker-end="url(#erd-many)" />
                    <path d="M550 150 L550 510" class="rel" marker-start="url(#erd-one)" marker-end="url(#erd-many)" />
                    <path d="M660 110 L710 110 L710 250 L760 250" class="rel" marker-start="url(#erd-one)"
                        marker-end="url(#erd-many)" />
                    <path d="M660 130 L690 130 L690 400 L760 400" class="rel" marker-start="url(#erd-one)"
                        marker-end="url(#erd-many)" />
                    <path d="M760 540 L730 540 L730 140 L660 140" class="rel" marker-start="url(#erd-one)"
                        marker-end="url(#erd-many)" />
                    <path d="M430 650 L430 320" class="rel rel-grain" marker-start="url(#erd-one)"
                        marker-end="url(#erd-many)" />
                    <path d="M150 760 L150 730" class="rel" marker-start="url(#erd-one)" marker-end="url(#erd-many)" />
                    <path d="M250 700 L390 700 L390 150" class="rel rel-soft" marker-end="url(#erd-many)" />

                    <g class="entity">
                        <rect x="350" y="70" width="310" height="80" rx="6" class="ent ent-hub" />
                        <rect x="350" y="70" width="310" height="24" rx="6" class="ent-head ent-head-hub" />
                        <text class="et" x="505" y="82" text-anchor="middle"
                            dominant-baseline="central">machine_master</text>
                        <text class="ek" x="362" y="108" dominant-baseline="central">PK id_machine &#183; 1 row /
                            machine</text>
                        <text class="ef" x="362" y="126" dominant-baseline="central">id_type, mould, material,
                            machine_name</text>
                        <text class="ec" x="362" y="143" dominant-baseline="central">live machine snapshot</text>
                    </g>

                    <g class="entity">
                        <rect x="30" y="60" width="220" height="90" rx="6" class="ent ent-key" />
                        <rect x="30" y="60" width="220" height="24" rx="6" class="ent-head ent-head-key" />
                        <text class="et" x="140" y="72" text-anchor="middle" dominant-baseline="central">sap</text>
                        <text class="ek" x="42" y="98" dominant-baseline="central">UQ id_type + mould</text>
                        <text class="ef" x="42" y="116" dominant-baseline="central">type, material, part_weight,
                            sap_ct</text>
                        <text class="ec" x="42" y="134" dominant-baseline="central">product master</text>
                    </g>

                    <g class="entity">
                        <rect x="350" y="220" width="310" height="100" rx="6" class="ent ent-log" />
                        <rect x="350" y="220" width="310" height="24" rx="6" class="ent-head ent-head-log" />
                        <text class="et" x="505" y="232" text-anchor="middle" dominant-baseline="central">report</text>
                        <text class="ek" x="362" y="258" dominant-baseline="central">PK id &#183; id_machine,
                            production_date, shift</text>
                        <text class="ef" x="362" y="276" dominant-baseline="central">id_type, mould, downtime
                            buckets</text>
                        <text class="ef" x="362" y="294" dominant-baseline="central">avail_hour, material_used
                            (computed)</text>
                        <text class="ec" x="362" y="311" dominant-baseline="central">aggregated shift report</text>
                    </g>

                    <g class="entity">
                        <rect x="350" y="370" width="310" height="76" rx="6" class="ent ent-log" />
                        <rect x="350" y="370" width="310" height="24" rx="6" class="ent-head ent-head-log" />
                        <text class="et" x="505" y="382" text-anchor="middle" dominant-baseline="central">reject</text>
                        <text class="ek" x="362" y="408" dominant-baseline="central">PK id &#183; id_machine,
                            production_date, shift</text>
                        <text class="ef" x="362" y="426" dominant-baseline="central">id_type, mould, reject_*
                            buckets</text>
                        <text class="ec" x="362" y="441" dominant-baseline="central">daily reject weights</text>
                    </g>

                    <g class="entity">
                        <rect x="350" y="510" width="310" height="90" rx="6" class="ent ent-log" />
                        <rect x="350" y="510" width="310" height="24" rx="6" class="ent-head ent-head-log" />
                        <text class="et" x="505" y="522" text-anchor="middle"
                            dominant-baseline="central">machine_log_{id}</text>
                        <text class="ek" x="362" y="548" dominant-baseline="central">PK id &#183; one table per
                            id_machine</text>
                        <text class="ef" x="362" y="566" dominant-baseline="central">category, problem, start, finish,
                            status_start</text>
                        <text class="ec" x="362" y="584" dominant-baseline="central">state-change log (0..N)</text>
                    </g>

                    <g class="entity">
                        <rect x="350" y="650" width="310" height="70" rx="6" class="ent ent-ref" />
                        <rect x="350" y="650" width="310" height="24" rx="6" class="ent-head ent-head-ref" />
                        <text class="et" x="505" y="662" text-anchor="middle"
                            dominant-baseline="central">calendar</text>
                        <text class="ek" x="362" y="688" dominant-baseline="central">NK production_date + shift
                            (heap)</text>
                        <text class="ef" x="362" y="706" dominant-baseline="central">day_type, planned_hours, start,
                            finish</text>
                    </g>

                    <g class="entity">
                        <rect x="760" y="220" width="210" height="70" rx="6" class="ent ent-log" />
                        <rect x="760" y="220" width="210" height="24" rx="6" class="ent-head ent-head-log" />
                        <text class="et" x="865" y="232" text-anchor="middle"
                            dominant-baseline="central">utilities</text>
                        <text class="ek" x="772" y="258" dominant-baseline="central">PK id &#183; id_machine</text>
                        <text class="ef" x="772" y="276" dominant-baseline="central">utility_name, start, finish</text>
                    </g>

                    <g class="entity">
                        <rect x="760" y="370" width="210" height="70" rx="6" class="ent ent-log" />
                        <rect x="760" y="370" width="210" height="24" rx="6" class="ent-head ent-head-log" />
                        <text class="et" x="865" y="382" text-anchor="middle" dominant-baseline="central">db_log</text>
                        <text class="ek" x="772" y="408" dominant-baseline="central">PK id &#183; id_machine
                            (nullable)</text>
                        <text class="ef" x="772" y="426" dominant-baseline="central">process, details,
                            error_message</text>
                    </g>

                    <g class="entity">
                        <rect x="760" y="510" width="210" height="70" rx="6" class="ent ent-key" />
                        <rect x="760" y="510" width="210" height="24" rx="6" class="ent-head ent-head-key" />
                        <text class="et" x="865" y="522" text-anchor="middle"
                            dominant-baseline="central">material_group</text>
                        <text class="ek" x="772" y="548" dominant-baseline="central">PK material</text>
                        <text class="ef" x="772" y="566" dominant-baseline="central">group_name, updated_at</text>
                    </g>

                    <g class="entity">
                        <rect x="30" y="640" width="220" height="90" rx="6" class="ent ent-people" />
                        <rect x="30" y="640" width="220" height="24" rx="6" class="ent-head ent-head-people" />
                        <text class="et" x="140" y="652" text-anchor="middle"
                            dominant-baseline="central">staff_list</text>
                        <text class="ek" x="42" y="678" dominant-baseline="central">PK id &#183; staff_id</text>
                        <text class="ef" x="42" y="696" dominant-baseline="central">machine_name ('/'-delimited)</text>
                        <text class="ef" x="42" y="714" dominant-baseline="central">work_shift, start/end_date</text>
                    </g>

                    <g class="entity">
                        <rect x="30" y="760" width="220" height="70" rx="6" class="ent ent-people" />
                        <rect x="30" y="760" width="220" height="24" rx="6" class="ent-head ent-head-people" />
                        <text class="et" x="140" y="772" text-anchor="middle"
                            dominant-baseline="central">attendance</text>
                        <text class="ek" x="42" y="798" dominant-baseline="central">PK id &#183; staff_id</text>
                        <text class="ef" x="42" y="816" dominant-baseline="central">status, machine_name, shift</text>
                    </g>

                    <rect x="700" y="640" width="280" height="200" rx="8" class="standalone-box" />
                    <text class="ec" x="716" y="662" dominant-baseline="central">Standalone (no relationship)</text>

                    <g class="entity">
                        <rect x="720" y="678" width="240" height="64" rx="6" class="ent ent-ref" />
                        <rect x="720" y="678" width="240" height="24" rx="6" class="ent-head ent-head-ref" />
                        <text class="et" x="840" y="690" text-anchor="middle"
                            dominant-baseline="central">app_setting</text>
                        <text class="ek" x="732" y="716" dominant-baseline="central">PK Key</text>
                        <text class="ef" x="732" y="734" dominant-baseline="central">Value, updated_at</text>
                    </g>

                    <g class="entity">
                        <rect x="720" y="756" width="240" height="64" rx="6" class="ent ent-ref" />
                        <rect x="720" y="756" width="240" height="24" rx="6" class="ent-head ent-head-ref" />
                        <text class="et" x="840" y="768" text-anchor="middle"
                            dominant-baseline="central">plc_passwords</text>
                        <text class="ek" x="732" y="794" dominant-baseline="central">PK department</text>
                        <text class="ef" x="732" y="812" dominant-baseline="central">password, updated_at</text>
                    </g>

                    <g>
                        <line x1="280" y1="795" x2="320" y2="795" class="rel" marker-end="url(#erd-many)" />
                        <text class="ec" x="326" y="795" dominant-baseline="central">app-enforced (no FK)</text>
                        <line x1="280" y1="815" x2="320" y2="815" class="rel rel-soft" marker-end="url(#erd-many)" />
                        <text class="ec" x="326" y="815" dominant-baseline="central">soft join (delimited)</text>
                        <line x1="480" y1="815" x2="520" y2="815" class="rel rel-grain" marker-end="url(#erd-many)" />
                        <text class="ec" x="526" y="815" dominant-baseline="central">time grain (date + shift)</text>
                    </g>
                </svg>
            </div>

            <v-divider class="my-6" />

            <div class="section-label mb-3">Table Reference</div>
            <v-row dense>
                <v-col v-for="table in tables" :key="table.name" cols="12" md="6" lg="4">
                    <v-card variant="outlined" class="h-100">
                        <div class="d-flex align-center px-3 py-2 table-card-head">
                            <v-icon size="18" class="mr-2" :color="table.color">{{ table.icon }}</v-icon>
                            <span class="text-body-2 font-weight-medium">{{ table.name }}</span>
                            <v-spacer />
                            <span class="text-caption text-medium-emphasis">{{ table.columns.length }} cols</span>
                        </div>
                        <v-divider />
                        <div class="table-card-body">
                            <div v-for="col in table.columns" :key="col.name"
                                class="d-flex align-center px-3 py-1 col-row">
                                <span class="col-name text-body-2">{{ col.name }}</span>
                                <v-spacer />
                                <span class="col-type text-caption text-medium-emphasis mr-2">{{ col.type }}</span>
                                <v-chip v-if="col.key" :color="keyColor(col.key)" size="x-small" label
                                    variant="tonal">{{ col.key }}</v-chip>
                            </div>
                        </div>
                    </v-card>
                </v-col>
            </v-row>

        </div>
    </div>
</template>

<script setup>
import imgDatabase from '@/assets/network/database.png'
import imgCentralPlc from '@/assets/network/omron-cj2m.jpg'
import imgPlc from '@/assets/network/omron-cp2e.png'

function keyColor(key) {
    if (key === 'PK') return 'primary';
    if (key === 'UQ') return 'teal';
    if (key === 'FK*') return 'orange';
    if (key === 'COMP') return 'purple';
    return 'grey';
}

const tables = [
    {
        name: 'machine_master', icon: 'mdi-robot-industrial', color: 'error',
        columns: [
            { name: 'id_machine', type: 'int', key: 'PK' },
            { name: 'shift', type: 'int' },
            { name: 'machine_name', type: 'nvarchar(max)' },
            { name: 'packer', type: 'nvarchar(max)' },
            { name: 'material', type: 'nvarchar(max)', key: 'FK*' },
            { name: 'id_type', type: 'int', key: 'FK*' },
            { name: 'mould', type: 'int', key: 'FK*' },
            { name: 'type', type: 'nvarchar(max)' },
            { name: 'category', type: 'nvarchar(max)' },
            { name: 'jo_no', type: 'nvarchar(max)' },
            { name: 'qty_perct', type: 'int' },
            { name: 'gross_weight', type: 'float' },
            { name: 'part_weight', type: 'float' },
            { name: 'shot', type: 'int' },
            { name: 'qty_order', type: 'int' },
            { name: 'wip_opening', type: 'int' },
            { name: 'wip_closing', type: 'int' },
            { name: 'shift_output', type: 'int', key: 'COMP' },
            { name: 'finish_good', type: 'int' },
            { name: 'inward', type: 'float', key: 'COMP' },
            { name: 'qty_accum', type: 'int' },
            { name: 'qty_balance', type: 'int', key: 'COMP' },
            { name: 'material_used', type: 'float', key: 'COMP' },
            { name: 'part_scrap', type: 'float' },
            { name: 'runner', type: 'float', key: 'COMP' },
            { name: 'act_ct', type: 'float' },
            { name: 'sap_ct', type: 'float' },
            { name: 'status_start', type: 'bit' },
            { name: 'status_off', type: 'bit' },
            { name: 'visual_qc', type: 'int' },
            { name: 'measure_qc', type: 'int' },
        ],
    },
    {
        name: 'report', icon: 'mdi-file-chart-outline', color: 'blue',
        columns: [
            { name: 'id', type: 'int', key: 'PK' },
            { name: 'id_machine', type: 'int', key: 'FK*' },
            { name: 'time', type: 'datetime' },
            { name: 'production_date', type: 'date' },
            { name: 'shift', type: 'int' },
            { name: 'machine_name', type: 'nvarchar(max)' },
            { name: 'packer', type: 'nvarchar(max)' },
            { name: 'material', type: 'nvarchar(max)' },
            { name: 'id_type', type: 'int', key: 'FK*' },
            { name: 'mould', type: 'int', key: 'FK*' },
            { name: 'type', type: 'nvarchar(max)' },
            { name: 'jo_no', type: 'nvarchar(max)' },
            { name: 'qty_perct', type: 'int' },
            { name: 'gross_weight', type: 'float' },
            { name: 'part_weight', type: 'float' },
            { name: 'shot', type: 'int' },
            { name: 'qty_order', type: 'int' },
            { name: 'wip_opening', type: 'int' },
            { name: 'wip_closing', type: 'int' },
            { name: 'shift_output', type: 'int', key: 'COMP' },
            { name: 'finish_good', type: 'int' },
            { name: 'inward', type: 'float', key: 'COMP' },
            { name: 'qty_accum', type: 'int' },
            { name: 'qty_balance', type: 'int', key: 'COMP' },
            { name: 'material_used', type: 'float', key: 'COMP' },
            { name: 'runner', type: 'float', key: 'COMP' },
            { name: 'reject_startup', type: 'float' },
            { name: 'reject_startup_per', type: 'float', key: 'COMP' },
            { name: 'reject_prod', type: 'float' },
            { name: 'reject_prod_per', type: 'float', key: 'COMP' },
            { name: 'act_ct', type: 'float' },
            { name: 'production_running', type: 'float' },
            { name: 'sap_ct', type: 'float' },
            { name: 'change_full_set', type: 'float' },
            { name: 'change_half_set', type: 'float' },
            { name: 'change_parts', type: 'float' },
            { name: 'maintenance_dt', type: 'float' },
            { name: 'technician_dt', type: 'float' },
            { name: 'production_dt', type: 'float' },
            { name: 'buyoff_dt', type: 'float' },
            { name: 'planned_dt', type: 'float' },
            { name: 'avail_hour', type: 'float', key: 'COMP' },
            { name: 'remark', type: 'nvarchar(max)' },
            { name: 'part_scrap', type: 'float' },
            { name: 'reject_labelling', type: 'float' },
            { name: 'reject_purging', type: 'float' },
            { name: 'reject_preform', type: 'float' },
            { name: 'reject_total_pcs', type: 'int' },
        ],
    },
    {
        name: 'reject', icon: 'mdi-alert-octagon-outline', color: 'blue',
        columns: [
            { name: 'id_machine', type: 'int', key: 'FK*' },
            { name: 'machine_name', type: 'nvarchar(max)' },
            { name: 'id_type', type: 'int', key: 'FK*' },
            { name: 'mould', type: 'int', key: 'FK*' },
            { name: 'total_weight', type: 'float' },
            { name: 'reject_panelling', type: 'float' },
            { name: 'reject_lumpy', type: 'float' },
            { name: 'reject_black_dot', type: 'float' },
            { name: 'reject_burst', type: 'float' },
            { name: 'reject_startup', type: 'float' },
            { name: 'reject_preform', type: 'float' },
            { name: 'reject_purging', type: 'float' },
            { name: 'reject_others', type: 'float' },
            { name: 'shift', type: 'int' },
            { name: 'production_date', type: 'date' },
            { name: 'id', type: 'int', key: 'PK' },
        ],
    },
    {
        name: 'machine_log_{id}', icon: 'mdi-history', color: 'blue',
        columns: [
            { name: 'id', type: 'int', key: 'PK' },
            { name: 'machine_name', type: 'nvarchar(max)' },
            { name: 'id_type', type: 'int', key: 'FK*' },
            { name: 'mould', type: 'int', key: 'FK*' },
            { name: 'start', type: 'datetime' },
            { name: 'finish', type: 'datetime' },
            { name: 'shot', type: 'int' },
            { name: 'category', type: 'nvarchar(max)' },
            { name: 'problem', type: 'nvarchar(max)' },
            { name: 'mould_category', type: 'int' },
            { name: 'shift', type: 'int' },
            { name: 'production_date', type: 'date' },
            { name: 'act_ct', type: 'float' },
            { name: 'status_start', type: 'bit' },
        ],
    },
    {
        name: 'sap', icon: 'mdi-database-outline', color: 'teal',
        columns: [
            { name: 'id', type: 'int', key: 'PK' },
            { name: 'id_type', type: 'int', key: 'UQ' },
            { name: 'mould', type: 'int', key: 'UQ' },
            { name: 'type', type: 'nvarchar(max)' },
            { name: 'qty_perct', type: 'int' },
            { name: 'process', type: 'nvarchar(max)' },
            { name: 'material', type: 'nvarchar(max)' },
            { name: 'part_weight', type: 'float' },
            { name: 'tolerance', type: 'float' },
            { name: 'gross_weight', type: 'float' },
            { name: 'sap_ct', type: 'float' },
        ],
    },
    {
        name: 'utilities', icon: 'mdi-fan', color: 'grey',
        columns: [
            { name: 'id_machine', type: 'int', key: 'FK*' },
            { name: 'machine_name', type: 'nvarchar(max)' },
            { name: 'utility_name', type: 'nvarchar(max)' },
            { name: 'start', type: 'datetime' },
            { name: 'finish', type: 'datetime' },
            { name: 'category', type: 'nvarchar(max)' },
            { name: 'shift', type: 'int' },
            { name: 'production_date', type: 'date' },
            { name: 'id', type: 'int', key: 'PK' },
        ],
    },
    {
        name: 'staff_list', icon: 'mdi-account-multiple-outline', color: 'amber-darken-2',
        columns: [
            { name: 'staff_id', type: 'int' },
            { name: 'staff_name', type: 'nvarchar(max)' },
            { name: 'staff_role', type: 'nvarchar(max)' },
            { name: 'status', type: 'nvarchar(max)' },
            { name: 'machine_name', type: 'nvarchar(max)', key: 'FK*' },
            { name: 'start_date', type: 'date' },
            { name: 'end_date', type: 'date' },
            { name: 'work_shift', type: 'int' },
            { name: 'production_date', type: 'date' },
            { name: 'shift', type: 'int' },
            { name: 'id', type: 'int', key: 'PK' },
        ],
    },
    {
        name: 'attendance', icon: 'mdi-account-clock-outline', color: 'amber-darken-2',
        columns: [
            { name: 'staff_id', type: 'int' },
            { name: 'staff_name', type: 'nvarchar(max)' },
            { name: 'staff_role', type: 'nvarchar(max)' },
            { name: 'status', type: 'nvarchar(max)' },
            { name: 'machine_name', type: 'nvarchar(max)', key: 'FK*' },
            { name: 'sv_id', type: 'int' },
            { name: 'sv_ll', type: 'int' },
            { name: 'production_date', type: 'date' },
            { name: 'shift', type: 'int' },
            { name: 'id', type: 'int', key: 'PK' },
        ],
    },
    {
        name: 'calendar', icon: 'mdi-calendar-outline', color: 'purple',
        columns: [
            { name: 'production_date', type: 'date', key: 'NK' },
            { name: 'shift', type: 'int', key: 'NK' },
            { name: 'day_type', type: 'nvarchar(max)' },
            { name: 'planned_hours', type: 'float' },
            { name: 'start', type: 'datetime' },
            { name: 'finish', type: 'datetime' },
        ],
    },
    {
        name: 'material_group', icon: 'mdi-shape-outline', color: 'teal',
        columns: [
            { name: 'material', type: 'nvarchar(255)', key: 'PK' },
            { name: 'group_name', type: 'nvarchar(20)' },
            { name: 'updated_at', type: 'datetime' },
        ],
    },
    {
        name: 'app_setting', icon: 'mdi-cog-outline', color: 'grey',
        columns: [
            { name: 'Key', type: 'nvarchar(100)', key: 'PK' },
            { name: 'Value', type: 'nvarchar(1000)' },
            { name: 'updated_at', type: 'datetime' },
        ],
    },
    {
        name: 'plc_passwords', icon: 'mdi-lock-outline', color: 'grey',
        columns: [
            { name: 'department', type: 'nvarchar(50)', key: 'PK' },
            { name: 'password', type: 'int' },
            { name: 'updated_at', type: 'datetime2(7)' },
        ],
    },
    {
        name: 'db_log', icon: 'mdi-database-eye-outline', color: 'blue',
        columns: [
            { name: 'id', type: 'int', key: 'PK' },
            { name: 'id_machine', type: 'int', key: 'FK*' },
            { name: 'time', type: 'datetime' },
            { name: 'process', type: 'nvarchar(max)' },
            { name: 'details', type: 'nvarchar(max)' },
            { name: 'error_message', type: 'nvarchar(max)' },
        ],
    },
];
</script>

<style scoped>
.section-label {
    font-size: 0.95rem;
    font-weight: 600;
    color: rgb(var(--v-theme-on-surface));
    margin-bottom: 12px;
}

.diagram-wrap {
    width: 100%;
    max-width: 720px;
    margin-inline: auto;
}

.erd-wrap {
    max-width: 980px;
    overflow-x: auto;
}

.diagram-svg {
    width: 100%;
    height: auto;
    display: block;
}

.table-card-head {
    background: rgba(var(--v-theme-on-surface), 0.03);
}

.table-card-body {
    max-height: 320px;
    overflow-y: auto;
}

.col-row:not(:last-child) {
    border-bottom: 1px solid rgba(var(--v-border-color), 0.12);
}

.col-name {
    font-family: 'Roboto Mono', ui-monospace, monospace;
}

.col-type {
    font-family: 'Roboto Mono', ui-monospace, monospace;
}

.box {
    fill: rgb(var(--v-theme-surface));
    stroke: rgba(var(--v-border-color), 0.38);
    stroke-width: 0.5;
}

.box-teal {
    fill: #E1F5EE;
    stroke: #0F6E56;
}

.box-blue {
    fill: #E6F1FB;
    stroke: #185FA5;
}

.box-coral {
    fill: #FAECE7;
    stroke: #993C1D;
}

.box-amber {
    fill: #FAEEDA;
    stroke: #854F0B;
}

.th {
    font-size: 14px;
    font-weight: 500;
    fill: rgb(var(--v-theme-on-surface));
}

.ts {
    font-size: 12px;
    font-weight: 400;
    fill: rgba(var(--v-theme-on-surface), 0.7);
}

.arr {
    stroke: rgba(var(--v-theme-on-surface), 0.55);
    stroke-width: 1.5;
    fill: none;
}

.bus {
    stroke: #D85A30;
    stroke-width: 1.5;
    fill: none;
}

.ent {
    fill: rgb(var(--v-theme-surface));
    stroke: rgba(var(--v-border-color), 0.5);
    stroke-width: 1;
}

.ent-head {
    fill: #ECEFF3;
    stroke: none;
}

.ent-hub {
    stroke: #993C1D;
}

.ent-head-hub {
    fill: #FAECE7;
}

.ent-key {
    stroke: #0F6E56;
}

.ent-head-key {
    fill: #E1F5EE;
}

.ent-log {
    stroke: #185FA5;
}

.ent-head-log {
    fill: #E6F1FB;
}

.ent-people {
    stroke: #854F0B;
}

.ent-head-people {
    fill: #FAEEDA;
}

.ent-ref {
    stroke: rgba(var(--v-border-color), 0.5);
}

.ent-head-ref {
    fill: #F0EEF4;
}

.et {
    font-size: 13px;
    font-weight: 600;
    fill: rgb(var(--v-theme-on-surface));
}

.ek {
    font-size: 11px;
    font-weight: 600;
    fill: rgba(var(--v-theme-on-surface), 0.85);
}

.ef {
    font-size: 11px;
    font-weight: 400;
    fill: rgba(var(--v-theme-on-surface), 0.7);
}

.ec {
    font-size: 10.5px;
    font-style: italic;
    fill: rgba(var(--v-theme-on-surface), 0.55);
}

.rel {
    stroke: rgba(var(--v-theme-on-surface), 0.5);
    stroke-width: 1.4;
    fill: none;
}

.rel-soft {
    stroke: #C0392B;
    stroke-dasharray: 5 4;
}

.rel-grain {
    stroke: #6C5CE7;
    stroke-dasharray: 2 3;
}

.standalone-box {
    fill: none;
    stroke: rgba(var(--v-theme-on-surface), 0.35);
    stroke-width: 1.2;
    stroke-dasharray: 6 4;
}
</style>