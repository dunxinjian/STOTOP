<template>
  <div class="rule-group-tree">
    <!-- 顶部工具栏 -->
    <div class="tree-toolbar">
      <a-input-search
        v-model:value="searchText"
        placeholder="搜索规则组"
        size="small"
        allow-clear
      />
      <div class="toolbar-actions">
        <a-tooltip title="新增组">
          <a-button type="text" size="small" @click="handleAddGroup">
            <PlusOutlined />
          </a-button>
        </a-tooltip>
        <a-tooltip title="导入组(JSON)">
          <a-button type="text" size="small" @click="showImportModal = true">
            <ImportOutlined />
          </a-button>
        </a-tooltip>
      </div>
    </div>

    <!-- 规则组列表 -->
    <div class="group-list">
      <div
        v-for="(group, index) in filteredGroups"
        :key="group.id"
        class="group-item"
        :class="{ active: store.selectedGroupId === group.id }"
        @click="selectGroup(group)"
        @contextmenu.prevent="onGroupContextMenu(group, $event)"
      >
        <div class="group-row">
          <span class="group-index">{{ index + 1 }}</span>
          <!-- 编辑名称 -->
          <template v-if="editingGroupId === group.id">
            <a-input
              v-model:value="editingName"
              size="small"
              style="flex: 1; min-width: 0;"
              @pressEnter="confirmRename"
              @blur="confirmRename"
              ref="renameInputRef"
            />
          </template>
          <template v-else>
            <span class="group-name" :title="group.name" @dblclick.stop="startRename(group)">{{ group.name }}</span>
          </template>
          <a-badge
            :count="group.lines.length"
            :show-zero="true"
            :number-style="{ backgroundColor: group.lines.length > 0 ? 'var(--color-info)' : 'var(--border)', fontSize: '11px' }"
          />
          <CompletenessBadge :score="getGroupScore(group)" />
          <HitRateBadge :rate="null" />
          <!-- 操作按钮 -->
          <div class="group-actions" @click.stop>
            <a-tooltip title="编辑名称">
              <a-button type="text" size="small" @click="startRename(group)">
                <EditOutlined />
              </a-button>
            </a-tooltip>
            <a-popconfirm
              title="确定删除整个规则组？"
              @confirm="handleDeleteGroup(group.id)"
              ok-text="确定"
              cancel-text="取消"
            >
              <a-button type="text" size="small" danger>
                <DeleteOutlined />
              </a-button>
            </a-popconfirm>
          </div>
        </div>

      </div>

      <!-- 空状态 -->
      <a-empty
        v-if="filteredGroups.length === 0"
        :image="simpleImage"
        description="暂无规则组"
      >
        <a-button type="dashed" size="small" @click="handleAddGroup">
          <PlusOutlined /> 新增规则组
        </a-button>
      </a-empty>
    </div>

    <!-- 导入 JSON 弹窗 -->
    <a-modal
      v-model:open="showImportModal"
      title="导入规则组 (JSON)"
      @ok="handleImportConfirm"
      ok-text="导入"
      cancel-text="取消"
      :width="520"
    >
      <p style="color: var(--text-3); font-size: 12px; margin-bottom: 8px;">
        粘贴规则组 JSON，可以是单个对象或数组
      </p>
      <a-textarea
        v-model:value="importJson"
        :rows="10"
        placeholder='{"name": "组名", "lines": [...]}'
      />
    </a-modal>

    <!-- 右键菜单 -->
    <div
      v-if="groupContextMenu.visible"
      class="context-menu"
      :style="{ top: groupContextMenu.y + 'px', left: groupContextMenu.x + 'px' }"
      @click="groupContextMenu.visible = false"
    >
      <a-menu size="small" @click="onGroupContextMenuClick">
        <a-menu-item key="copyGroup"><CopyOutlined /> 复制整组</a-menu-item>
        <a-menu-item key="cutGroup"><ScissorOutlined /> 剪切整组</a-menu-item>
        <a-menu-item key="pasteGroup" :disabled="!store.clipboard || store.clipboard.type !== 'group'"><SnippetsOutlined /> 粘贴组</a-menu-item>
        <a-menu-item key="cloneGroup"><BlockOutlined /> 克隆整组</a-menu-item>
        <a-menu-divider />
        <a-menu-item key="deleteGroup" danger><DeleteOutlined /> 删除组</a-menu-item>
      </a-menu>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick } from 'vue'
import { PlusOutlined, ImportOutlined, EditOutlined, DeleteOutlined, CopyOutlined, ScissorOutlined, SnippetsOutlined, BlockOutlined } from '@ant-design/icons-vue'
import { Empty, message } from 'ant-design-vue'
import { useAutoVoucherRuleStore } from '@/stores/autoVoucherRule'
import type { RuleGroup } from '@/stores/autoVoucherRule'
import { useConfigCompleteness } from '../composables/useConfigCompleteness'
import CompletenessBadge from './CompletenessBadge.vue'
import HitRateBadge from './HitRateBadge.vue'

const simpleImage = Empty.PRESENTED_IMAGE_SIMPLE
const store = useAutoVoucherRuleStore()

// 配置完整度引擎
const { ruleCompleteness } = useConfigCompleteness(
  computed(() => store.formData),
  store,
)

const searchText = ref('')
const editingGroupId = ref<string | null>(null)
const editingName = ref('')
const renameInputRef = ref<any>(null)
const showImportModal = ref(false)
const importJson = ref('')

const filteredGroups = computed(() => {
  const groups = store.formData.ruleGroups
  if (!searchText.value) return groups
  const keyword = searchText.value.toLowerCase()
  return groups.filter(g => g.name.toLowerCase().includes(keyword))
})

function selectGroup(group: RuleGroup) {
  store.selectedGroupId = group.id
  store.selectedLineId = null
}

function handleAddGroup() {
  store.addGroup()
}

function handleDeleteGroup(groupId: string) {
  store.removeGroup(groupId)
}

function startRename(group: RuleGroup) {
  editingGroupId.value = group.id
  editingName.value = group.name
  nextTick(() => {
    renameInputRef.value?.focus?.()
  })
}

function confirmRename() {
  if (editingGroupId.value && editingName.value.trim()) {
    store.updateGroupName(editingGroupId.value, editingName.value.trim())
  }
  editingGroupId.value = null
}


function handleImportConfirm() {
  if (!importJson.value.trim()) {
    message.warning('请输入 JSON 内容')
    return
  }
  const ok = store.importGroup(importJson.value)
  if (ok) {
    message.success('导入成功')
    showImportModal.value = false
    importJson.value = ''
  } else {
    message.error('JSON 解析失败，请检查格式')
  }
}

// ==================== 右键菜单 ====================
const groupContextMenu = ref({ visible: false, x: 0, y: 0, groupId: '' })

function onGroupContextMenu(group: RuleGroup, e: MouseEvent) {
  e.preventDefault()
  groupContextMenu.value = {
    visible: true,
    x: e.clientX,
    y: e.clientY,
    groupId: group.id,
  }
  const handler = () => { groupContextMenu.value.visible = false; document.removeEventListener('click', handler) }
  setTimeout(() => document.addEventListener('click', handler), 0)
}

function onGroupContextMenuClick({ key }: { key: string | number }) {
  const id = groupContextMenu.value.groupId
  switch (String(key)) {
    case 'copyGroup': store.clipboardCopyGroup(id); break
    case 'cutGroup': store.clipboardCutGroup(id); break
    case 'pasteGroup': store.clipboardPasteGroup(); break
    case 'cloneGroup': store.cloneGroup(id); break
    case 'deleteGroup': store.removeGroup(id); break
  }
}

/** 获取组的完整度分数 */
function getGroupScore(group: RuleGroup): number | null {
  const groups = store.formData?.ruleGroups
  if (!groups || !ruleCompleteness.value.groupScores.length) return null
  const idx = groups.findIndex((g: RuleGroup) => g.id === group.id)
  if (idx < 0 || idx >= ruleCompleteness.value.groupScores.length) return null
  return ruleCompleteness.value.groupScores[idx].score
}
</script>

<style lang="scss" scoped>
.rule-group-tree {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
  background: var(--bg-card);
  border-radius: 8px;
  border: 1px solid var(--border);
}

.tree-toolbar {
  padding: 12px;
  border-bottom: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.toolbar-actions {
  display: flex;
  gap: 4px;
}

.group-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.group-item {
  padding: 6px 10px;
  border-radius: 6px;
  cursor: pointer;
  margin-bottom: 4px;
  transition: all 0.2s;
  border: 1px solid transparent;

  &:hover {
    background: var(--bg-muted);
    .group-actions { opacity: 1; }
  }

  &.active {
    background: var(--bg-muted);
    border-color: var(--border-strong);
    .group-actions { opacity: 1; }
  }
}

.group-row {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.group-index {
  width: 20px;
  height: 20px;
  border-radius: 4px;
  background: var(--bg-muted);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  color: var(--text-3);
  flex-shrink: 0;
}

.group-name {
  flex: 1;
  min-width: 0;
  font-size: 13px;
  color: var(--text-1);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  cursor: text;
}

.group-actions {
  opacity: 0;
  transition: opacity 0.2s;
  display: flex;
  flex-shrink: 0;
  gap: 0;
}


.context-menu {
  position: fixed;
  z-index: 1050;
  background: var(--bg-card);
  border-radius: 6px;
  box-shadow: var(--shadow-md);
  padding: 4px 0;

  :deep(.ant-menu) {
    border: none;
    box-shadow: none;
  }
}
</style>
