<template>
    <v-card flat class="pa-0">
        <div class="diagram-wrap">
            <svg viewBox="0 0 760 640" preserveAspectRatio="xMidYMid meet" role="img"
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
                </defs>

                <!-- Client -->
                <g class="node">
                    <rect x="255" y="30" width="170" height="52" rx="8" class="box" />
                    <text class="th" x="340" y="50" text-anchor="middle" dominant-baseline="central">CMS.Client</text>
                    <text class="ts" x="340" y="68" text-anchor="middle" dominant-baseline="central">Vue 3 &#183; /api
                        proxy</text>
                </g>
                <line x1="340" y1="82" x2="340" y2="112" class="arr" marker-end="url(#net-arrow)" />

                <!-- Server -->
                <g class="node">
                    <rect x="235" y="114" width="210" height="56" rx="8" class="box box-teal" />
                    <text class="th" x="340" y="134" text-anchor="middle" dominant-baseline="central">CMS.Server</text>
                    <text class="ts" x="340" y="152" text-anchor="middle" dominant-baseline="central">.NET 8 Web API
                        &#183;
                        IIS</text>
                </g>

                <!-- Server to DB -->
                <line x1="445" y1="142" x2="572" y2="142" class="arr" marker-end="url(#net-arrow)" />
                <g class="node">
                    <rect x="560" y="116" width="110" height="52" rx="8" class="box box-blue" />
                    <image :href="imgDatabase" x="576" y="116" width="52" height="52" clip-path="url(#clip-db)"
                        preserveAspectRatio="xMidYMid slice" />
                    <text class="th" x="632" y="134" text-anchor="middle" dominant-baseline="central">SQL Server</text>
                    <text class="ts" x="632" y="152" text-anchor="middle" dominant-baseline="central">raw ADO.NET</text>
                </g>

                <line x1="340" y1="170" x2="340" y2="204" class="arr" marker-end="url(#net-arrow)" />

                <!-- Central PLC -->
                <g class="node">
                    <rect x="230" y="206" width="220" height="56" rx="8" class="box box-coral" />
                    <image :href="imgCentralPlc" x="246" y="206" width="56" height="56" clip-path="url(#clip-central)"
                        preserveAspectRatio="xMidYMid slice" />
                    <text class="th" x="360" y="226" text-anchor="middle" dominant-baseline="central">Central PLC
                        (CJ2M)</text>
                    <text class="ts" x="360" y="244" text-anchor="middle" dominant-baseline="central">172.17.86.80
                        &#183;
                        aggregator</text>
                </g>

                <text class="ts" x="340" y="288" text-anchor="middle">FINS/UDP &#183; &#8804;250 words per read</text>

                <!-- Trunk and bus -->
                <line x1="340" y1="262" x2="340" y2="326" class="bus" />
                <line x1="115" y1="326" x2="625" y2="326" class="bus" />
                <line x1="115" y1="326" x2="115" y2="352" class="arr bus" marker-end="url(#net-arrow)" />
                <line x1="285" y1="326" x2="285" y2="352" class="arr bus" marker-end="url(#net-arrow)" />
                <line x1="455" y1="326" x2="455" y2="352" class="arr bus" marker-end="url(#net-arrow)" />
                <line x1="625" y1="326" x2="625" y2="352" class="arr bus" marker-end="url(#net-arrow)" />

                <!-- Machine sub-PLCs -->
                <g class="node">
                    <rect x="55" y="356" width="120" height="52" rx="8" class="box box-amber" />
                    <image :href="imgPlc" x="71" y="356" width="44" height="44" clip-path="url(#clip-m1)"
                        preserveAspectRatio="xMidYMid slice" />
                    <text class="th" x="127" y="374" text-anchor="middle" dominant-baseline="central">Machine 1</text>
                    <text class="ts" x="127" y="392" text-anchor="middle" dominant-baseline="central">.86.221</text>
                </g>
                <g class="node">
                    <rect x="225" y="356" width="120" height="52" rx="8" class="box box-amber" />
                    <image :href="imgPlc" x="241" y="356" width="44" height="44" clip-path="url(#clip-m2)"
                        preserveAspectRatio="xMidYMid slice" />
                    <text class="th" x="297" y="374" text-anchor="middle" dominant-baseline="central">Machine 2</text>
                    <text class="ts" x="297" y="392" text-anchor="middle" dominant-baseline="central">.86.222</text>
                </g>
                <g class="node">
                    <rect x="395" y="356" width="120" height="52" rx="8" class="box box-amber" />
                    <image :href="imgPlc" x="411" y="356" width="44" height="44" clip-path="url(#clip-mn)"
                        preserveAspectRatio="xMidYMid slice" />
                    <text class="th" x="467" y="374" text-anchor="middle" dominant-baseline="central">Machine n</text>
                    <text class="ts" x="467" y="392" text-anchor="middle" dominant-baseline="central">.86.220+n</text>
                </g>
                <g class="node">
                    <rect x="565" y="356" width="120" height="52" rx="8" class="box" />
                    <image :href="imgPlc" x="581" y="356" width="44" height="44" clip-path="url(#clip-mt)"
                        preserveAspectRatio="xMidYMid slice" />
                    <text class="th" x="637" y="374" text-anchor="middle" dominant-baseline="central">Machine
                        Test</text>
                    <text class="ts" x="625" y="392" text-anchor="middle" dominant-baseline="central">.86.220</text>
                </g>

                <!-- Log tables -->
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

                <!-- Legend -->
                <rect x="55" y="536" width="14" height="14" rx="3" class="box box-coral" />
                <text class="ts" x="76" y="543" dominant-baseline="central">Central aggregator (CJ2M)</text>
                <rect x="290" y="536" width="14" height="14" rx="3" class="box box-amber" />
                <text class="ts" x="311" y="543" dominant-baseline="central">Machine sub-PLC (CP2E)</text>
                <rect x="500" y="536" width="14" height="14" rx="3" class="box box-blue" />
                <text class="ts" x="521" y="543" dominant-baseline="central">Log table</text>
            </svg>
        </div>
    </v-card>
</template>

<script setup>
import imgDatabase from '@/assets/network/database.png'
import imgCentralPlc from '@/assets/network/omron-cj2m.jpg'
import imgPlc from '@/assets/network/omron-cp2e.png'
</script>

<style scoped>
.diagram-wrap {
    width: 100%;
    max-width: 720px;
    margin-inline: auto;
}

.diagram-svg {
    width: 100%;
    height: auto;
    display: block;
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
</style>