<template>
    <v-dialog :model-value="modelValue" max-width="420" @update:model-value="$emit('update:modelValue', $event)">
        <v-card>
            <v-card-title class="bg-primary text-white">Mould Setup Targets (hrs)</v-card-title>
            <v-card-text class="pt-4">
                <v-text-field v-model.number="form.blow" type="number" step="0.1" label="Blow Mould" density="compact"
                    variant="outlined" class="mb-2" />
                <v-text-field v-model.number="form.half" type="number" step="0.1" label="Half Set" density="compact"
                    variant="outlined" class="mb-2" />
                <v-text-field v-model.number="form.full" type="number" step="0.1" label="Full Set" density="compact"
                    variant="outlined" />
            </v-card-text>
            <v-card-actions>
                <v-spacer />
                <v-btn variant="text" @click="$emit('update:modelValue', false)">Cancel</v-btn>
                <v-btn color="primary" variant="elevated" :loading="saving" @click="save">Save</v-btn>
            </v-card-actions>
        </v-card>
    </v-dialog>
</template>

<script setup>
import { ref, watch } from 'vue';

const props = defineProps({
    modelValue: { type: Boolean, default: false },
});
const emit = defineEmits(['update:modelValue', 'saved']);

const form = ref({ blow: 1.5, half: 3.0, full: 5.5 });
const saving = ref(false);

async function load() {
    const res = await fetch('/api/oee/mould-targets');
    if (!res.ok) return;
    form.value = await res.json();
}

async function save() {
    saving.value = true;
    try {
        const res = await fetch('/api/oee/mould-targets', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(form.value),
        });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        emit('saved');
        emit('update:modelValue', false);
    } catch (err) {
        console.error('[MouldTargets] save:', err);
    } finally {
        saving.value = false;
    }
}

watch(() => props.modelValue, (open) => { if (open) load(); });
</script>