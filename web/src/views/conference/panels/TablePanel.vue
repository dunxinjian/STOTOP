<template>
  <div class="table-panel">
    <!-- 智能操作栏 -->
    <SmartActionBar description="基于社交关系、职级、饮食偏好智能编排桌次座位">
      <a-button type="primary" @click="handleAutoArrange" :loading="arranging">
        <ThunderboltOutlined />智能编桌
      </a-button>
      <a-button @click="showAddTableModal"><PlusOutlined />添加桌次</a-button>
      <a-button @click="handleExportImage" :loading="exportingImg">
        <PictureOutlined />导出桌次图
      </a-button>
      <a-button @click="handleExportPdf" :loading="exportingPdf">
        <FilePdfOutlined />导出PDF
      </a-button>
      <a-button @click="handleExportExcel" :loading="exportingExcel">
        <FileExcelOutlined />导出Excel
      </a-button>
      <a-button @click="openManualArrange">
        <DragOutlined />手动编排
      </a-button>
    </SmartActionBar>

    <!-- 选择餐次 -->
    <div class="table-panel__meal-select">
      <span class="table-panel__meal-label">选择餐次：</span>
      <a-select
        v-model:value="selectedMealId"
        placeholder="请先选择餐次"
        style="width: 280px"
        @change="loadTables"
        :loading="loadingMeals"
      >
        <a-select-option v-for="m in mealPlans" :key="m.id" :value="m.id">
          {{ m.date?.substring(0, 10) }} {{ m.mealType }} - {{ m.location || '未指定地点' }}
        </a-select-option>
      </a-select>
    </div>

    <!-- 统计栏 -->
    <a-row :gutter="16" style="margin: 16px 0" v-if="selectedMealId">
      <a-col :span="6"><StatCard title="桌数" :value="tables.length" :clickable="false" /></a-col>
      <a-col :span="6"><StatCard title="已入座" :value="seatedCount" :clickable="false" /></a-col>
      <a-col :span="6"><StatCard title="待安排" :value="availableAttendees.length" :clickable="false" /></a-col>
      <a-col :span="6"><StatCard title="平均每桌" :value="avgPerTable" suffix="人" :clickable="false" /></a-col>
    </a-row>

    <!-- 左右布局：桌次视图 + 待安排人员面板 -->
    <div class="table-panel__layout" v-if="selectedMealId">
      <!-- 左侧：桌次视图 -->
      <div class="table-panel__main">
        <!-- 搜索定位 -->
        <a-input-search
          v-if="tables.length"
          v-model:value="searchKeyword"
          placeholder="搜索姓名定位桌次"
          style="width: 250px; margin-bottom: 16px"
          allow-clear
        />

        <!-- 双视图切换 -->
        <a-tabs v-model:activeKey="viewMode">
          <a-tab-pane key="visual" tab="圆桌视图">
            <a-spin :spinning="loadingTables">
              <RoundTable
                :tables="roundTableData"
                :search-keyword="searchKeyword"
                :draggable="true"
                :sortable="true"
                @seat-click="handleSeatClick"
                @seat-drop="handleSeatDrop"
                @table-reorder="handleTableReorder"
              />
            </a-spin>
          </a-tab-pane>
          <a-tab-pane key="list" tab="列表视图">
            <a-table
              :columns="tableListColumns"
              :data-source="tables"
              row-key="id"
              :loading="loadingTables"
              :pagination="false"
              size="small"
              :expandable="{ expandedRowRender }"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'capacity'">
                  {{ record.seats?.length || 0 }} / {{ record.seatCount }}
                </template>
                <template v-else-if="column.dataIndex === 'guests'">
                  <template v-if="(record.seats?.map((s: TableSeatDto) => s.attendeeName).filter(Boolean) || []).length === 0">
                    <span style="color: #999">暂无</span>
                  </template>
                  <template v-else>
                    <a-tooltip v-if="record.seats.map((s: TableSeatDto) => s.attendeeName).filter(Boolean).join('、').length > 20">
                      <template #title>
                        {{ record.seats.map((s: TableSeatDto) => s.attendeeName).filter(Boolean).join('、') }}
                      </template>
                      {{ record.seats.map((s: TableSeatDto) => s.attendeeName).filter(Boolean).slice(0, 3).join('、') }}…
                    </a-tooltip>
                    <span v-else>
                      {{ record.seats.map((s: TableSeatDto) => s.attendeeName).filter(Boolean).join('、') }}
                    </span>
                  </template>
                </template>
                <template v-else-if="column.dataIndex === 'vip'">
                  <a-tag v-if="record.remark?.includes('VIP')" color="gold">VIP</a-tag>
                  <span v-else>-</span>
                </template>
                <template v-else-if="column.key === 'action'">
                  <a-space>
                    <a @click="openManageSeats(record as TableDto)">管理座位</a>
                    <a @click="handleEditTable(record as TableDto)">编辑</a>
                    <a-popconfirm title="确定删除此桌次？" @confirm="handleDeleteTable(record.id)">
                      <a class="danger-link">删除</a>
                    </a-popconfirm>
                  </a-space>
                </template>
              </template>
              <template #expandedRowRender="{ record }">
                <a-table
                  :columns="seatColumns"
                  :data-source="record.seats || []"
                  row-key="id"
                  :pagination="false"
                  size="small"
                  :show-header="true"
                />
              </template>
            </a-table>
          </a-tab-pane>
        </a-tabs>
      </div>

      <!-- 右侧：待安排人员面板 -->
      <div class="table-panel__sidebar">
        <a-card size="small" :title="`待安排人员 (${availableAttendees.length})`">
          <template #extra>
            <a-badge :count="availableAttendees.length" :overflow-count="999" />
          </template>
          <a-input-search
            v-model:value="sidebarSearch"
            placeholder="搜索姓名/单位"
            style="margin-bottom: 12px"
            allow-clear
            size="small"
          />
          <div class="table-panel__sidebar-list">
            <a-empty v-if="!filteredAvailableAttendees.length" description="暂无待安排人员" :image="simpleImage" />
            <div
              v-for="a in filteredAvailableAttendees"
              :key="a.id"
              class="table-panel__sidebar-item"
            >
              <div class="table-panel__sidebar-info">
                <div class="table-panel__sidebar-name">{{ a.name }}</div>
                <div class="table-panel__sidebar-meta">{{ a.organization || '-' }}</div>
              </div>
              <a-dropdown :trigger="['click']">
                <a-button type="link" size="small">安排入座</a-button>
                <template #overlay>
                  <a-menu @click="({ key }: any) => handleQuickAssign(a.id, Number(key))">
                    <a-menu-item v-for="t in tables" :key="t.id">
                      {{ t.tableName || `第${t.tableNumber}桌` }}
                      ({{ t.seats?.length || 0 }}/{{ t.seatCount }})
                    </a-menu-item>
                  </a-menu>
                </template>
              </a-dropdown>
            </div>
          </div>
        </a-card>
      </div>
    </div>

    <a-empty v-if="!selectedMealId" description="请先选择一个餐次" style="margin-top: 60px" />

    <!-- 编辑桌次 Modal -->
    <a-modal
      v-model:open="tableModalVisible"
      :title="editingTable ? '编辑桌次' : '添加桌次'"
      @ok="handleSaveTable"
      :confirm-loading="savingTable"
      :width="480"
    >
      <a-form :model="tableForm" layout="vertical" ref="tableFormRef">
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="桌号" name="tableNumber" :rules="[{ required: true, message: '请输入桌号' }]">
              <a-input-number v-model:value="tableForm.tableNumber" :min="1" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="最大座位数" name="seatCount" :rules="[{ required: true, message: '请输入座位数' }]">
              <a-input-number v-model:value="tableForm.seatCount" :min="1" :max="20" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="桌名" name="tableName">
          <a-input v-model:value="tableForm.tableName" placeholder="如：主桌、贵宾桌" />
        </a-form-item>
        <a-form-item label="备注" name="remark">
          <a-input v-model:value="tableForm.remark" placeholder="备注，如VIP标记" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 座位管理 Popover -->
    <a-modal
      v-model:open="seatPopoverVisible"
      :title="currentSeat?.attendeeName ? '座位详情' : '安排座位'"
      :width="360"
      :footer="null"
    >
      <template v-if="currentSeat?.attendeeName">
        <p><strong>姓名：</strong>{{ currentSeat.attendeeName }}</p>
        <p><strong>单位：</strong>{{ currentSeat.organization || '-' }}</p>
        <p><strong>职务：</strong>{{ currentSeat.role || '-' }}</p>
        <a-space>
          <a-button danger size="small" @click="handleRemoveSeat" :loading="savingSeat">移除</a-button>
          <a-dropdown :trigger="['click']">
            <a-button size="small"><SwapOutlined />换桌</a-button>
            <template #overlay>
              <a-menu @click="({ key }: any) => handleMoveToTable(currentSeat!.attendeeId, currentTableId, Number(key))">
                <a-menu-item
                  v-for="t in tables.filter(t => t.id !== currentTableId)"
                  :key="t.id"
                >
                  {{ t.tableName || `第${t.tableNumber}桌` }}
                  ({{ t.seats?.length || 0 }}/{{ t.seatCount }})
                </a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </a-space>
      </template>
      <template v-else>
        <a-select
          v-model:value="assignAttendeeId"
          placeholder="选择人员"
          style="width: 100%; margin-bottom: 12px"
          show-search
          :filter-option="filterOption"
        >
          <a-select-option v-for="a in availableAttendees" :key="a.id" :value="a.id">
            {{ a.name }} - {{ a.organization || '' }}
          </a-select-option>
        </a-select>
        <a-button type="primary" size="small" @click="handleAssignSeat" :loading="savingSeat" :disabled="!assignAttendeeId">
          确认安排
        </a-button>
      </template>
    </a-modal>

    <!-- 手动编排弹窗 -->
    <a-modal
      v-model:open="showManualModal"
      title="手动编排桌次"
      :width="1000"
      @ok="saveManualArrangement"
      :confirm-loading="savingManual"
      ok-text="保存"
      cancel-text="取消"
    >
      <template #footer>
        <div class="manual-footer">
          <div class="manual-footer__stats">
            已安排 {{ manualSeatedCount }} 人 / 待安排 {{ localUnassigned.length }} 人
          </div>
          <div class="manual-footer__actions">
            <a-button @click="resetManualArrangement">重置</a-button>
            <a-button @click="showManualModal = false">取消</a-button>
            <a-button type="primary" :loading="savingManual" @click="saveManualArrangement">保存</a-button>
          </div>
        </div>
      </template>
      <div class="manual-arrange">
        <!-- 左侧：桌次列表 -->
        <div class="manual-arrange__left">
          <draggable
            :list="tables"
            item-key="id"
            :animation="200"
            handle=".manual-arrange__table-handle"
            ghost-class="manual-table-ghost"
            class="manual-arrange__tables"
            @end="onManualTableReorderEnd"
          >
           <template #item="{ element: table }">
              <div class="manual-arrange__table-card" :class="`is-${getTableStatus(table.id)}`">
                <div class="manual-arrange__table-header" @dblclick="openReorderInput(table)" title="双击调整桌次顺序">
                  <span class="manual-arrange__table-handle" title="拖拽调整桌次顺序">
                    <DragOutlined />
                  </span>
                  <span class="manual-arrange__table-name">
                    {{ table.tableName || `第${table.tableNumber}桌` }}
                  </span>
                  <span class="manual-arrange__table-count">
                    {{ getTableSeatCount(table.id) }} / {{ MAX_PER_TABLE }}
                    <span v-if="isTableFull(table.id)" class="manual-arrange__table-full-tag">已满</span>
                  </span>
                </div>
                <div class="manual-arrange__table-search">
                  <a-select
                    :value="attendeeSelectValues[table.id] ?? null"
                    show-search
                    allow-clear
                    :filter-option="filterAttendeeOption"
                    :options="attendeeSelectOptions"
                    placeholder="搜索姓名/单位 添加到本桌"
                    size="small"
                    style="width: 100%;"
                    :dropdown-match-select-width="320"
                    @change="(val: any) => handleSelectAttendee(table.id, val)"
                  />
                </div>
                <draggable
                  :list="localTableSeats.get(table.id) || []"
                  :group="{ name: 'seats', pull: true, put: () => !isTableFull(table.id) }"
                  item-key="attendeeId"
                  :animation="150"
                  class="drag-zone drag-zone--seats"
                  ghost-class="ghost"
                  @change="() => onTableDragChange(table.id)"
                >
                  <template #item="{ element }">
                    <a-tag closable @close.prevent="removeFromTable(table.id, element)" class="drag-tag drag-tag--seat">
                      {{ element.attendeeName }}
                    </a-tag>
                  </template>
                  <template #footer>
                    <div v-if="!(localTableSeats.get(table.id) || []).length" class="drag-zone__placeholder">
                      拖拽人员到此处
                    </div>
                  </template>
                </draggable>
              </div>
            </template>
          </draggable>
        </div>
        <!-- 右侧：待安排人员 -->
        <div class="manual-arrange__right">
          <div class="manual-arrange__right-header">
            待安排人员（{{ localUnassigned.length }}人）
          </div>
          <a-input-search
            v-model:value="manualSearch"
            placeholder="搜索姓名/单位"
            size="small"
            allow-clear
            style="margin-bottom: 8px"
          />
          <draggable
            :list="localUnassigned"
            :group="{ name: 'seats', pull: true, put: true }"
            item-key="attendeeId"
            :animation="150"
            class="drag-zone drag-zone--unassigned"
            ghost-class="ghost"
          >
            <template #item="{ element }">
              <a-tag class="drag-tag drag-tag--unassigned">
                <span>{{ element.attendeeName }}</span>
                <span class="drag-tag__org">{{ element.organization || '' }}</span>
              </a-tag>
            </template>
            <template #footer>
              <div v-if="!localUnassigned.length" class="drag-zone__placeholder">
                所有人员已安排
              </div>
            </template>
          </draggable>
        </div>
      </div>
    </a-modal>

    <!-- 双击桌头：调整桌次顺序输入弹窗 -->
    <a-modal
      v-model:open="showReorderInput"
      title="调整桌次顺序"
      :width="360"
      :ok-text="'确定'"
      :cancel-text="'取消'"
      @ok="confirmReorderInput"
      destroy-on-close
    >
      <div style="margin-bottom: 8px;">
        将 <b>{{ reorderTargetName }}</b> 调整到第几桌？
      </div>
      <a-input-number
        v-model:value="reorderTargetNumber"
        :min="1"
        :max="tables.length || 1"
        :precision="0"
        style="width: 100%;"
        autofocus
        @press-enter="confirmReorderInput"
      />
      <div style="color: #999; margin-top: 8px; font-size: 12px;">
        范围 1 ~ {{ tables.length }}，确定后其他桌会自动重新编号并重命名为“第X桌”
      </div>
    </a-modal>

    <!-- 管理座位弹窗 -->
    <a-modal
      v-model:open="showManageModal"
      title="管理座位"
      :width="720"
      @ok="confirmManageSeats"
      :confirm-loading="savingManage"
      ok-text="保存"
      cancel-text="取消"
    >
      <div class="manage-seats">
        <!-- 左侧：当前桌已入座 -->
        <div class="manage-seats__left">
          <div class="manage-seats__title">
            已入座 ({{ managingSeats.length }} / {{ managingTable?.seatCount || 0 }})
          </div>
          <a-empty v-if="!managingSeats.length" description="暂无入座人员" :image="simpleImage" />
          <div v-for="s in managingSeats" :key="s.attendeeId" class="manage-seats__item">
            <div class="manage-seats__info">
              <span class="manage-seats__seat-num">{{ s.seatNumber }}号</span>
              <span>{{ getAttendeeName(s.attendeeId) }}</span>
              <span class="manage-seats__org">{{ getAttendeeOrg(s.attendeeId) }}</span>
            </div>
            <a-button type="link" danger size="small" @click="removeFromManaging(s.attendeeId)">移除</a-button>
          </div>
        </div>
        <!-- 右侧：可用人员 -->
        <div class="manage-seats__right">
          <div class="manage-seats__title">可安排人员</div>
          <a-input-search
            v-model:value="manageSearch"
            placeholder="搜索姓名/单位"
            size="small"
            allow-clear
            style="margin-bottom: 8px"
          />
          <div class="manage-seats__available-list">
            <a-empty v-if="!manageAvailable.length" description="无可用人员" :image="simpleImage" />
            <div v-for="a in manageAvailable" :key="a.id" class="manage-seats__item">
              <div class="manage-seats__info">
                <span>{{ a.name }}</span>
                <span class="manage-seats__org">{{ a.organization || '-' }}</span>
              </div>
              <a-button type="link" size="small" @click="addToManaging(a.id)">添加</a-button>
            </div>
          </div>
        </div>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, h, reactive } from 'vue'
import { message, Empty, Tooltip } from 'ant-design-vue'
import {
  ThunderboltOutlined, PlusOutlined, PictureOutlined, FilePdfOutlined, FileExcelOutlined, SwapOutlined, DragOutlined,
} from '@ant-design/icons-vue'
import draggable from 'vuedraggable'

const simpleImage = Empty.PRESENTED_IMAGE_SIMPLE
import SmartActionBar from '../components/SmartActionBar.vue'
import StatCard from '../components/StatCard.vue'
import RoundTable from '../components/RoundTable.vue'
import type { SeatData, TableData } from '../components/RoundTable.vue'
import {
  getMealPlans, getTables, createTable, updateTable, deleteTable,
  autoArrangeTables, setTableSeats, exportTablesImage, exportTablesPdf, exportTablesExcel,
  getAttendees
} from '@/api/conference'
import type {
  MealPlanListItemDto, TableDto, CreateTableRequest, TableSeatDto,
  AttendeeListItemDto, SeatInput, MealAttendeeDto
} from '@/api/conference'

const props = defineProps<{ eventId: number; eventData?: any }>()

const loadingMeals = ref(false)
const loadingTables = ref(false)
const arranging = ref(false)
const exportingImg = ref(false)
const exportingPdf = ref(false)
const exportingExcel = ref(false)
const savingTable = ref(false)
const savingSeat = ref(false)

const mealPlans = ref<MealPlanListItemDto[]>([])
const selectedMealId = ref<number | undefined>(undefined)
const tables = ref<TableDto[]>([])
const viewMode = ref('visual')
const searchKeyword = ref('')

// Stats
const seatedCount = computed(() => tables.value.reduce((sum, t) => sum + (t.seats?.length || 0), 0))
const unseatedCount = computed(() => availableAttendees.value.length)
const avgPerTable = computed(() => {
  if (!tables.value.length) return 0
  return Math.round(seatedCount.value / tables.value.length)
})

// RoundTable data adapter
const roundTableData = computed<TableData[]>(() =>
  tables.value.map((t) => ({
    id: t.id,
    name: t.tableName || `第${t.tableNumber}桌`,
    maxSeats: t.seatCount,
    seats: (t.seats || []).map((s) => ({
      id: s.id,
      attendeeId: s.attendeeId,
      attendeeName: s.attendeeName || '空位',
      company: s.organization,
      position: s.seatNumber,
    })),
  }))
)

// Table Modal
const tableModalVisible = ref(false)
const editingTable = ref<TableDto | null>(null)
const tableFormRef = ref()
const tableForm = ref<CreateTableRequest>({
  tableNumber: 1,
  tableName: '',
  seatCount: 10,
  remark: '',
})

// Seat management
const seatPopoverVisible = ref(false)
const currentSeat = ref<TableSeatDto | null>(null)
const currentTableId = ref<number>(0)
const assignAttendeeId = ref<number | undefined>(undefined)
const allAttendees = ref<AttendeeListItemDto[]>([])
const mealPlanAttendees = ref<MealAttendeeDto[]>([])
const availableAttendees = computed(() => {
  const seated = new Set<number>()
  tables.value.forEach((t) => t.seats?.forEach((s) => seated.add(s.attendeeId)))
  // 优先使用当前餐食计划的已安排人员，fallback 到全部参会人员
  if (mealPlanAttendees.value.length > 0) {
    return mealPlanAttendees.value
      .filter((a) => !seated.has(a.attendeeId))
      .map((a) => ({ id: a.attendeeId, name: a.name, organization: a.organization } as AttendeeListItemDto))
  }
  return allAttendees.value.filter((a) => !seated.has(a.id))
})

// Table columns
const tableListColumns = [
  { title: '桌号', dataIndex: 'tableNumber', width: 80 },
  { title: '桌名', dataIndex: 'tableName', width: 120, ellipsis: true },
  { title: '人数/容量', dataIndex: 'capacity', width: 100 },
  { title: '人员', dataIndex: 'guests', width: 200, ellipsis: true },
  { title: 'VIP', dataIndex: 'vip', width: 80 },
  { title: '操作', key: 'action', width: 120 },
]

const seatColumns = [
  { title: '座位号', dataIndex: 'seatNumber', width: 80 },
  { title: '姓名', dataIndex: 'attendeeName', width: 120 },
  { title: '单位', dataIndex: 'organization', width: 160, ellipsis: true },
  { title: '职务', dataIndex: 'role', width: 120 },
]

// Used in expandedRowRender slot attribute
const expandedRowRender = (_record: any) => undefined // handled by template slot

// Sidebar search
const sidebarSearch = ref('')
const filteredAvailableAttendees = computed(() => {
  const kw = sidebarSearch.value.trim().toLowerCase()
  if (!kw) return availableAttendees.value
  return availableAttendees.value.filter(
    (a) => a.name?.toLowerCase().includes(kw) || a.organization?.toLowerCase().includes(kw)
  )
})

// Manage seats modal
const showManageModal = ref(false)
const managingTable = ref<TableDto | null>(null)
const managingSeats = ref<SeatInput[]>([])
const manageSearch = ref('')
const savingManage = ref(false)

const manageAvailable = computed(() => {
  const inManaging = new Set(managingSeats.value.map((s) => s.attendeeId))
  // Also exclude people seated at OTHER tables
  const seatedElsewhere = new Set<number>()
  tables.value.forEach((t) => {
    if (t.id !== managingTable.value?.id) {
      t.seats?.forEach((s) => seatedElsewhere.add(s.attendeeId))
    }
  })
  const allSource = mealPlanAttendees.value.length > 0
    ? mealPlanAttendees.value.map((a) => ({ id: a.attendeeId, name: a.name, organization: a.organization } as AttendeeListItemDto))
    : allAttendees.value
  let list = allSource.filter((a) => !inManaging.has(a.id) && !seatedElsewhere.has(a.id))
  const kw = manageSearch.value.trim().toLowerCase()
  if (kw) {
    list = list.filter((a) => a.name?.toLowerCase().includes(kw) || a.organization?.toLowerCase().includes(kw))
  }
  return list
})

function openManageSeats(table: TableDto) {
  managingTable.value = table
  managingSeats.value = table.seats?.map((s) => ({
    attendeeId: s.attendeeId,
    seatNumber: s.seatNumber,
    remark: s.remark,
  })) || []
  manageSearch.value = ''
  showManageModal.value = true
}

function addToManaging(attendeeId: number) {
  const nextSeat = managingSeats.value.length > 0
    ? Math.max(...managingSeats.value.map((s) => s.seatNumber)) + 1
    : 1
  managingSeats.value.push({ attendeeId, seatNumber: nextSeat })
}

function removeFromManaging(attendeeId: number) {
  managingSeats.value = managingSeats.value.filter((s) => s.attendeeId !== attendeeId)
}

async function confirmManageSeats() {
  if (!managingTable.value) return
  savingManage.value = true
  try {
    await setTableSeats(managingTable.value.id, { seats: managingSeats.value })
    showManageModal.value = false
    await loadTables()
    message.success('座位已更新')
  } catch {
    message.error('保存失败')
  } finally {
    savingManage.value = false
  }
}

function getAttendeeName(attendeeId: number): string {
  if (mealPlanAttendees.value.length > 0) {
    const a = mealPlanAttendees.value.find((x) => x.attendeeId === attendeeId)
    if (a) return a.name
  }
  return allAttendees.value.find((x) => x.id === attendeeId)?.name || `ID:${attendeeId}`
}

function getAttendeeOrg(attendeeId: number): string {
  if (mealPlanAttendees.value.length > 0) {
    const a = mealPlanAttendees.value.find((x) => x.attendeeId === attendeeId)
    if (a) return a.organization || '-'
  }
  return allAttendees.value.find((x) => x.id === attendeeId)?.organization || '-'
}

// Quick assign from sidebar
async function handleQuickAssign(attendeeId: number, tableId: number) {
  try {
    const tbl = tables.value.find((t) => t.id === tableId)
    const existingSeats: SeatInput[] = (tbl?.seats || []).map((s) => ({
      attendeeId: s.attendeeId,
      seatNumber: s.seatNumber,
    }))
    const nextSeatNum = existingSeats.length ? Math.max(...existingSeats.map((s) => s.seatNumber)) + 1 : 1
    existingSeats.push({ attendeeId, seatNumber: nextSeatNum })
    await setTableSeats(tableId, { seats: existingSeats })
    message.success('安排成功')
    await loadTables()
  } catch {
    message.error('安排失败')
  }
}

// Move to another table
// ====== 圆桌视图：宾客拖拽换桌 ======
async function handleSeatDrop(payload: { seat: any; sourceTableId: number; targetTableId: number }) {
  const { seat, sourceTableId, targetTableId } = payload
  if (sourceTableId === targetTableId) return

  const targetTable = tables.value.find((t) => t.id === targetTableId)
  const sourceTable = tables.value.find((t) => t.id === sourceTableId)
  if (!targetTable || !sourceTable) return

  // 容量校验
  if ((targetTable.seats?.length || 0) >= targetTable.seatCount) {
    message.warning('目标桌已满')
    return
  }

  try {
    // 源桌移除该宾客
    const sourceSeats: SeatInput[] = (sourceTable.seats || [])
      .filter((s) => s.attendeeId !== seat.attendeeId)
      .map((s, i) => ({ attendeeId: s.attendeeId, seatNumber: i + 1 }))

    // 目标桌添加该宾客
    const targetSeats: SeatInput[] = [
      ...(targetTable.seats || []).map((s, i) => ({ attendeeId: s.attendeeId, seatNumber: i + 1 })),
      { attendeeId: seat.attendeeId, seatNumber: (targetTable.seats?.length || 0) + 1 },
    ]

    await Promise.all([
      setTableSeats(sourceTableId, { seats: sourceSeats }),
      setTableSeats(targetTableId, { seats: targetSeats }),
    ])

    message.success('移动成功')
    await loadTables()
  } catch {
    message.error('移动失败')
  }
}

// ====== 圆桌视图：桌次排序 ======
async function handleTableReorder(tableIds: number[]) {
  try {
    const promises: Promise<any>[] = []
    tableIds.forEach((id, index) => {
      const newNumber = index + 1
      const tbl = tables.value.find((t) => t.id === id)
      if (tbl && tbl.tableNumber !== newNumber) {
        promises.push(
          updateTable(id, {
            tableNumber: newNumber,
            tableName: tbl.tableName,
            seatCount: tbl.seatCount,
            remark: tbl.remark,
          })
        )
      }
    })
    if (promises.length > 0) {
      await Promise.all(promises)
      message.success('桌次顺序已更新')
      await loadTables()
    }
  } catch {
    message.error('排序更新失败')
  }
}

async function handleMoveToTable(attendeeId: number, fromTableId: number, toTableId: number) {
  const fromTable = tables.value.find((t) => t.id === fromTableId)
  const toTable = tables.value.find((t) => t.id === toTableId)
  if (!fromTable || !toTable) return

  const fromSeats = fromTable.seats?.filter((s) => s.attendeeId !== attendeeId)
    .map((s) => ({ attendeeId: s.attendeeId, seatNumber: s.seatNumber })) || []

  const maxSeat = toTable.seats?.length ? Math.max(...toTable.seats.map((s) => s.seatNumber)) : 0
  const toSeats = [
    ...(toTable.seats?.map((s) => ({ attendeeId: s.attendeeId, seatNumber: s.seatNumber })) || []),
    { attendeeId, seatNumber: maxSeat + 1 },
  ]

  try {
    await setTableSeats(fromTableId, { seats: fromSeats })
    await setTableSeats(toTableId, { seats: toSeats })
    seatPopoverVisible.value = false
    await loadTables()
    message.success('换桌成功')
  } catch {
    message.error('换桌失败')
  }
}

async function loadMealPlans() {
  loadingMeals.value = true
  try {
    const res: any = await getMealPlans(props.eventId)
    mealPlans.value = res ?? []
    if (mealPlans.value.length && !selectedMealId.value) {
      selectedMealId.value = mealPlans.value[0].id
      loadTables()
    }
    updateMealPlanAttendees()
  } catch {
    message.error('加载餐次列表失败')
  } finally {
    loadingMeals.value = false
  }
}

function updateMealPlanAttendees() {
  if (!selectedMealId.value) {
    mealPlanAttendees.value = []
    return
  }
  const plan = mealPlans.value.find((m) => m.id === selectedMealId.value)
  mealPlanAttendees.value = plan?.attendees ?? []
}

async function loadTables() {
  if (!selectedMealId.value) return
  loadingTables.value = true
  updateMealPlanAttendees()
  try {
    const res: any = await getTables(selectedMealId.value)
    tables.value = res ?? []
  } catch {
    message.error('加载桌次数据失败')
  } finally {
    loadingTables.value = false
  }
}

async function loadAttendees() {
  try {
    const res: any = await getAttendees(props.eventId, { pageSize: 9999 })
    allAttendees.value = res?.items ?? res ?? []
  } catch {
    allAttendees.value = []
  }
}

const handleAutoArrange = async () => {
  if (!selectedMealId.value) {
    message.warning('请先选择餐次')
    return
  }
  arranging.value = true
  try {
    await autoArrangeTables(selectedMealId.value, {})
    message.success('智能编桌完成')
    loadTables()
  } catch {
    message.error('智能编桌失败')
  } finally {
    arranging.value = false
  }
}

const handleExportImage = async () => {
  if (!selectedMealId.value) { message.warning('请先选择餐次'); return }
  exportingImg.value = true
  try {
    const blob: any = await exportTablesImage(selectedMealId.value)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url; a.download = '桌次图.png'; a.click()
    URL.revokeObjectURL(url)
  } catch {
    message.error('导出失败')
  } finally {
    exportingImg.value = false
  }
}

const handleExportPdf = async () => {
  if (!selectedMealId.value) { message.warning('请先选择餐次'); return }
  exportingPdf.value = true
  try {
    const blob: any = await exportTablesPdf(selectedMealId.value)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url; a.download = '桌次安排.pdf'; a.click()
    URL.revokeObjectURL(url)
  } catch {
    message.error('导出失败')
  } finally {
    exportingPdf.value = false
  }
}

const handleExportExcel = async () => {
  if (!selectedMealId.value) { message.warning('请先选择餐次'); return }
  exportingExcel.value = true
  try {
    const blob: any = await exportTablesExcel(selectedMealId.value)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url; a.download = '桌次编排.xlsx'; a.click()
    URL.revokeObjectURL(url)
  } catch {
    message.error('导出失败')
  } finally {
    exportingExcel.value = false
  }
}

function showAddTableModal() {
  if (!selectedMealId.value) { message.warning('请先选择餐次'); return }
  editingTable.value = null
  tableForm.value = { tableNumber: tables.value.length + 1, tableName: '', seatCount: 10, remark: '' }
  tableModalVisible.value = true
}

function handleEditTable(record: TableDto) {
  editingTable.value = record
  tableForm.value = {
    tableNumber: record.tableNumber,
    tableName: record.tableName || '',
    seatCount: record.seatCount,
    remark: record.remark || '',
  }
  tableModalVisible.value = true
}

async function handleSaveTable() {
  try {
    await tableFormRef.value?.validateFields()
  } catch { return }

  savingTable.value = true
  try {
    if (editingTable.value) {
      await updateTable(editingTable.value.id, tableForm.value)
      message.success('更新成功')
    } else {
      await createTable(selectedMealId.value!, tableForm.value)
      message.success('创建成功')
    }
    tableModalVisible.value = false
    loadTables()
  } catch {
    message.error('保存失败')
  } finally {
    savingTable.value = false
  }
}

async function handleDeleteTable(id: number) {
  try {
    await deleteTable(id)
    message.success('删除成功')
    loadTables()
  } catch {
    message.error('删除失败')
  }
}

function handleSeatClick(seat: SeatData, table: TableData) {
  const tbl = tables.value.find((t) => t.id === table.id)
  if (!tbl) return
  const seatDto = tbl.seats?.find((s) => s.id === seat.id)
  currentSeat.value = seatDto || { id: 0, tableId: tbl.id, attendeeId: 0, seatNumber: seat.position || 0, attendeeName: '', organization: '', role: '', remark: '' }
  currentTableId.value = tbl.id
  assignAttendeeId.value = undefined
  seatPopoverVisible.value = true
}

async function handleRemoveSeat() {
  if (!currentSeat.value) return
  savingSeat.value = true
  try {
    const tbl = tables.value.find((t) => t.id === currentTableId.value)
    const remainingSeats: SeatInput[] = (tbl?.seats || [])
      .filter((s) => s.id !== currentSeat.value!.id)
      .map((s) => ({ attendeeId: s.attendeeId, seatNumber: s.seatNumber }))
    await setTableSeats(currentTableId.value, { seats: remainingSeats })
    message.success('已移除')
    seatPopoverVisible.value = false
    loadTables()
  } catch {
    message.error('操作失败')
  } finally {
    savingSeat.value = false
  }
}

async function handleAssignSeat() {
  if (!assignAttendeeId.value) return
  savingSeat.value = true
  try {
    const tbl = tables.value.find((t) => t.id === currentTableId.value)
    const existingSeats: SeatInput[] = (tbl?.seats || []).map((s) => ({
      attendeeId: s.attendeeId,
      seatNumber: s.seatNumber,
    }))
    const nextSeatNum = existingSeats.length ? Math.max(...existingSeats.map((s) => s.seatNumber)) + 1 : 1
    existingSeats.push({ attendeeId: assignAttendeeId.value, seatNumber: nextSeatNum })
    await setTableSeats(currentTableId.value, { seats: existingSeats })
    message.success('安排成功')
    seatPopoverVisible.value = false
    loadTables()
  } catch {
    message.error('操作失败')
  } finally {
    savingSeat.value = false
  }
}

function filterOption(input: string, option: any) {
  const label = option.children?.[0]?.children || ''
  return String(label).toLowerCase().includes(input.toLowerCase())
}

// ====== 手动编排 ======
const showManualModal = ref(false)
const savingManual = ref(false)
const manualSearch = ref('')
const localTableSeats = ref<Map<number, LocalSeatItem[]>>(new Map())
const localUnassigned = ref<LocalSeatItem[]>([])

// 每桌硬上限：超过该值不允许拖入
const MAX_PER_TABLE = 10

function getTableSeatCount(tableId: number): number {
  return (localTableSeats.value.get(tableId) || []).length
}

function isTableFull(tableId: number): boolean {
  return getTableSeatCount(tableId) >= MAX_PER_TABLE
}

function getTableStatus(tableId: number): 'empty' | 'under' | 'full' {
  const n = getTableSeatCount(tableId)
  if (n === 0) return 'empty'
  if (n >= MAX_PER_TABLE) return 'full'
  return 'under'
}

// 手动编排弹窗：桌内搜索添加宾客
const attendeeSelectValues = reactive<Record<number, any>>({})

const attendeeSelectOptions = computed(() => {
  const src = mealPlanAttendees.value.length > 0
    ? mealPlanAttendees.value.map((a) => ({
        attendeeId: a.attendeeId,
        name: a.name,
        organization: a.organization || '',
      }))
    : allAttendees.value.map((a) => ({
        attendeeId: a.id,
        name: a.name || '',
        organization: a.organization || '',
      }))
  // 建立宾客当前所在桌映射
  const locationMap = new Map<number, string>()
  localTableSeats.value.forEach((seats, tid) => {
    const t = tables.value.find((x) => x.id === tid)
    const tName = t ? (t.tableName || `第${t.tableNumber}桌`) : '某桌'
    seats.forEach((s) => locationMap.set(s.attendeeId, tName))
  })
  return src.map((a) => {
    const loc = locationMap.get(a.attendeeId) || '未安排'
    return {
      value: a.attendeeId,
      label: `${a.name}　·　${a.organization || '-'}　[${loc}]`,
      name: a.name,
      organization: a.organization,
    }
  })
})

function filterAttendeeOption(input: string, option: any) {
  if (!input) return true
  const kw = input.toLowerCase()
  return (
    (option.name || '').toLowerCase().includes(kw) ||
    (option.organization || '').toLowerCase().includes(kw)
  )
}

function handleSelectAttendee(tableId: number, attendeeId: number | null | undefined) {
  // 立即重置选中值，保持搜索框可连续操作
  attendeeSelectValues[tableId] = null
  if (attendeeId == null) return
  addAttendeeToTable(tableId, attendeeId)
}

function addAttendeeToTable(tableId: number, attendeeId: number) {
  // 1. 从其他桌移除，并获取宾客信息
  let item: LocalSeatItem | null = null
  for (const [tid, seats] of localTableSeats.value.entries()) {
    const i = seats.findIndex((x) => x.attendeeId === attendeeId)
    if (i >= 0) {
      if (tid === tableId) {
        message.info('该宾客已在本桌')
        return
      }
      item = seats.splice(i, 1)[0]
      break
    }
  }
  // 2. 未在任何桌，从 unassigned 或源数据构造
  if (!item) {
    const u = localUnassigned.value.find((x) => x.attendeeId === attendeeId)
    if (u) {
      item = { ...u }
    } else {
      const src = mealPlanAttendees.value.find((a) => a.attendeeId === attendeeId)
      if (src) {
        item = {
          attendeeId: src.attendeeId,
          attendeeName: src.name,
          organization: src.organization || '',
        }
      } else {
        const a = allAttendees.value.find((x) => x.id === attendeeId)
        if (a) {
          item = {
            attendeeId: a.id,
            attendeeName: a.name || '',
            organization: a.organization || '',
          }
        }
      }
    }
  }
  if (!item) {
    message.warning('找不到该宾客')
    return
  }
  // 3. 加入目标桌，超员时先挑出本桌原最后一人到未安排
  const seats = localTableSeats.value.get(tableId) || []
  if (seats.length >= MAX_PER_TABLE) {
    const popped = seats.pop()
    if (popped) {
      message.info(`本桌已满，原最后一人「${popped.attendeeName}」已移至未安排列表`)
    }
  }
  seats.push(item)
  localTableSeats.value.set(tableId, seats)
  // 4. 重算未安排列表
  updateLocalUnassigned()
  message.success(`已将「${item.attendeeName}」加入本桌`)
}

interface LocalSeatItem {
  attendeeId: number
  attendeeName: string
  organization?: string
}

// 原始快照，用于重置
let snapshotTableSeats = new Map<number, LocalSeatItem[]>()
let snapshotUnassigned: LocalSeatItem[] = []

const manualSeatedCount = computed(() => {
  let count = 0
  localTableSeats.value.forEach((seats) => { count += seats.length })
  return count
})

const filteredLocalUnassigned = computed(() => {
  const kw = manualSearch.value.trim().toLowerCase()
  if (!kw) return localUnassigned.value
  return localUnassigned.value.filter(
    (a) => a.attendeeName.toLowerCase().includes(kw) || a.organization?.toLowerCase().includes(kw)
  )
})

function buildLocalData() {
  const map = new Map<number, LocalSeatItem[]>()
  tables.value.forEach((t) => {
    map.set(
      t.id,
      (t.seats || []).map((s) => ({
        attendeeId: s.attendeeId,
        attendeeName: s.attendeeName || '',
        organization: s.organization || '',
      }))
    )
  })
  localTableSeats.value = map
  updateLocalUnassigned()
}

function updateLocalUnassigned() {
  const seated = new Set<number>()
  localTableSeats.value.forEach((seats) => seats.forEach((s) => seated.add(s.attendeeId)))
  const allSource: LocalSeatItem[] = mealPlanAttendees.value.length > 0
    ? mealPlanAttendees.value.map((a) => ({
        attendeeId: a.attendeeId,
        attendeeName: a.name,
        organization: a.organization || '',
      }))
    : allAttendees.value.map((a) => ({
        attendeeId: a.id,
        attendeeName: a.name || '',
        organization: a.organization || '',
      }))
  localUnassigned.value = allSource.filter((a) => !seated.has(a.attendeeId))
}

function openManualArrange() {
  if (!selectedMealId.value) { message.warning('请先选择餐次'); return }
  if (!tables.value.length) { message.warning('请先添加桌次'); return }
  buildLocalData()
  // 保存快照
  snapshotTableSeats = new Map<number, LocalSeatItem[]>()
  localTableSeats.value.forEach((seats, id) => {
    snapshotTableSeats.set(id, seats.map((s) => ({ ...s })))
  })
  snapshotUnassigned = localUnassigned.value.map((s) => ({ ...s }))
  manualSearch.value = ''
  showManualModal.value = true
}

function onTableDragChange(tableId: number) {
  // vuedraggable 已自动更新 list，只需同步 unassigned
  updateLocalUnassigned()
}

// 双击桌头：调整桌次顺序输入弹窗状态
const showReorderInput = ref(false)
const reorderTargetTableId = ref<number | null>(null)
const reorderTargetNumber = ref<number>(1)
const reorderTargetName = computed(() => {
  const t = tables.value.find((x) => x.id === reorderTargetTableId.value)
  if (!t) return ''
  return t.tableName || `第${t.tableNumber}桌`
})

function openReorderInput(table: TableDto) {
  reorderTargetTableId.value = table.id
  reorderTargetNumber.value = table.tableNumber
  showReorderInput.value = true
}

async function confirmReorderInput() {
  const tableId = reorderTargetTableId.value
  const target = Number(reorderTargetNumber.value)
  if (!tableId || !target) {
    showReorderInput.value = false
    return
  }
  const total = tables.value.length
  if (target < 1 || target > total) {
    message.warning(`桌次必须在 1 ~ ${total} 之间`)
    return
  }
  const fromIdx = tables.value.findIndex((t) => t.id === tableId)
  const toIdx = target - 1
  if (fromIdx < 0) {
    showReorderInput.value = false
    return
  }
  if (fromIdx !== toIdx) {
    const [moved] = tables.value.splice(fromIdx, 1)
    tables.value.splice(toIdx, 0, moved)
  }
  showReorderInput.value = false
  // 复用拖拽排序逻辑：重编号 + 重命名为“第X桌” + 持久化
  await onManualTableReorderEnd()
}

// 手动编排弹窗内：桌次拖拽排序完成后，自动重编号并持久化
async function onManualTableReorderEnd() {
  const changes: Array<{ id: number; newNumber: number; newName: string; t: TableDto }> = []
  tables.value.forEach((t, idx) => {
    const newNumber = idx + 1
    const newName = `第${newNumber}桌`
    if (t.tableNumber !== newNumber || t.tableName !== newName) {
      changes.push({ id: t.id, newNumber, newName, t })
      // 本地立即生效：名称强制更新为“第X桌”
      t.tableNumber = newNumber
      t.tableName = newName
    }
  })
  if (!changes.length) return
  try {
    await Promise.all(
      changes.map((c) =>
        updateTable(c.id, {
          tableNumber: c.newNumber,
          tableName: c.newName,
          seatCount: c.t.seatCount,
          remark: c.t.remark,
        })
      )
    )
    message.success('桌次顺序与名称已更新')
  } catch {
    message.error('桌次顺序更新失败')
  }
}

function removeFromTable(tableId: number, item: LocalSeatItem) {
  const seats = localTableSeats.value.get(tableId)
  if (seats) {
    const idx = seats.findIndex((s) => s.attendeeId === item.attendeeId)
    if (idx >= 0) seats.splice(idx, 1)
  }
  localUnassigned.value.push({ ...item })
}

function resetManualArrangement() {
  const map = new Map<number, LocalSeatItem[]>()
  snapshotTableSeats.forEach((seats, id) => {
    map.set(id, seats.map((s) => ({ ...s })))
  })
  localTableSeats.value = map
  localUnassigned.value = snapshotUnassigned.map((s) => ({ ...s }))
}

async function saveManualArrangement() {
  // 硬上限校验：任何桌不得超过 MAX_PER_TABLE
  for (const table of tables.value) {
    const seats = localTableSeats.value.get(table.id) || []
    if (seats.length > MAX_PER_TABLE) {
      message.error(`${table.tableName || `第${table.tableNumber}桌`} 人数超过上限 ${MAX_PER_TABLE} 人，无法保存`)
      return
    }
  }
  savingManual.value = true
  try {
    const promises: Promise<any>[] = []
    for (const table of tables.value) {
      const localSeats = localTableSeats.value.get(table.id) || []
      const originalIds = new Set((table.seats || []).map((s) => s.attendeeId))
      const newIds = new Set(localSeats.map((s) => s.attendeeId))
      // 检查是否有变化
      const hasChange = originalIds.size !== newIds.size ||
        [...originalIds].some((id) => !newIds.has(id))
      if (hasChange) {
        const seats: SeatInput[] = localSeats.map((s, i) => ({
          attendeeId: s.attendeeId,
          seatNumber: i + 1,
        }))
        promises.push(setTableSeats(table.id, { seats }))
      }
    }
    if (promises.length > 0) {
      await Promise.all(promises)
      message.success(`已更新 ${promises.length} 张桌次`)
      await loadTables()
    } else {
      message.info('没有变化')
    }
    showManualModal.value = false
  } catch {
    message.error('保存失败')
  } finally {
    savingManual.value = false
  }
}

onMounted(() => {
  loadMealPlans()
  loadAttendees()
})
</script>

<style scoped lang="scss">
.table-panel {
  padding: 0;

  &__meal-select {
    display: flex;
    align-items: center;
    margin-bottom: 12px;
  }

  &__meal-label {
    font-size: 14px;
    color: #595959;
    margin-right: 8px;
    white-space: nowrap;
  }

  &__layout {
    display: flex;
    gap: 16px;
  }

  &__main {
    flex: 1;
    min-width: 0;
  }

  &__sidebar {
    width: 300px;
    flex-shrink: 0;
  }

  &__sidebar-list {
    max-height: 520px;
    overflow-y: auto;
  }

  &__sidebar-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 6px 0;
    border-bottom: 1px solid #f0f0f0;

    &:last-child {
      border-bottom: none;
    }
  }

  &__sidebar-info {
    flex: 1;
    min-width: 0;
  }

  &__sidebar-name {
    font-size: 13px;
    font-weight: 500;
    color: #262626;
  }

  &__sidebar-meta {
    font-size: 12px;
    color: #8c8c8c;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
}

.danger-link {
  color: var(--color-danger);
}

.manage-seats {
  display: flex;
  gap: 16px;
  min-height: 360px;

  &__left,
  &__right {
    flex: 1;
    min-width: 0;
  }

  &__left {
    border-right: 1px solid #f0f0f0;
    padding-right: 16px;
  }

  &__title {
    font-weight: 500;
    font-size: 14px;
    margin-bottom: 12px;
    color: #262626;
  }

  &__item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 5px 0;
    border-bottom: 1px solid #f5f5f5;

    &:last-child {
      border-bottom: none;
    }
  }

  &__info {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 13px;
  }

  &__seat-num {
    color: var(--color-info);
    font-weight: 500;
    min-width: 32px;
  }

  &__org {
    color: #8c8c8c;
    font-size: 12px;
  }

  &__available-list {
    max-height: 280px;
    overflow-y: auto;
  }
}

/* 手动编排弹窗 */
.manual-arrange {
  display: flex;
  gap: 16px;
  min-height: 400px;
  max-height: 60vh;

  &__left {
    flex: 7;
    min-width: 0;
    overflow-y: auto;
    padding-right: 12px;
    border-right: 1px solid #f0f0f0;
  }

  &__right {
    flex: 3;
    min-width: 0;
    display: flex;
    flex-direction: column;
  }

  &__right-header {
    font-weight: 500;
    font-size: 14px;
    margin-bottom: 8px;
    color: #262626;
  }

  &__tables {
    display: flex;
    flex-direction: column;
    gap: 12px;
  }

  &__table-card {
    border: 1px solid #e8e8e8;
    border-radius: 6px;
    transition: border-color 0.2s, background-color 0.2s;

    &.is-empty {
      border-color: #d9d9d9;
    }

    &.is-under {
      border-color: var(--color-warning);
      background: var(--color-warning-light);

      .manual-arrange__table-header {
        background: var(--color-warning-light);
        border-bottom-color: #ffe7ba;
      }

      .manual-arrange__table-count {
        color: var(--color-warning);
        font-weight: 500;
      }
    }

    &.is-full {
      border-color: var(--color-success);
      background: var(--color-success-light);

      .manual-arrange__table-header {
        background: var(--color-success-light);
        border-bottom-color: #d9f7be;
      }

      .manual-arrange__table-count {
        color: var(--color-success-text);
        font-weight: 500;
      }

      /* 已满时，drag-zone 禁用视觉 */
      .drag-zone--seats {
        background: rgba(82, 196, 26, 0.04);
      }
    }
  }

  &__table-full-tag {
    display: inline-block;
    margin-left: 6px;
    padding: 0 6px;
    font-size: 11px;
    line-height: 16px;
    color: #fff;
    background: var(--color-success);
    border-radius: 8px;
  }

  &__table-search {
    padding: 6px 10px;
    background: #fff;
    border-bottom: 1px solid #f5f5f5;
  }

  &__table-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 6px 12px;
    background: #fafafa;
    border-bottom: 1px solid #f0f0f0;
    border-top-left-radius: 6px;
    border-top-right-radius: 6px;
    user-select: none;
  }

  &__table-handle {
    display: inline-flex;
    align-items: center;
    cursor: grab;
    color: #bfbfbf;
    margin-right: 8px;
    font-size: 14px;
    transition: color 0.2s;

    &:hover { color: var(--color-primary); }
    &:active { cursor: grabbing; }
  }

  &__table-name {
    flex: 1;
    font-weight: 500;
    font-size: 13px;
    color: #262626;
  }

  &__table-count {
    font-size: 12px;
    color: #8c8c8c;
  }
}

.drag-zone {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  min-height: 40px;
  padding: 8px;
  border: 1px dashed transparent;
  border-radius: 4px;
  transition: border-color 0.3s;

  &:hover {
    border-color: var(--color-primary);
  }

  &--unassigned {
    flex: 1;
    overflow-y: auto;
    align-content: flex-start;
  }

  &__placeholder {
    width: 100%;
    text-align: center;
    color: #bfbfbf;
    font-size: 12px;
    line-height: 24px;
  }
}

.drag-tag {
  cursor: grab;
  user-select: none;
  margin: 0 !important;

  &--seat {
    flex: 1 1 80px;
    min-width: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    text-align: center;
    box-sizing: border-box;
  }

  &--unassigned {
    display: inline-flex;
    align-items: center;
    gap: 4px;
  }

  &__org {
    font-size: 11px;
    color: #8c8c8c;
  }
}

.ghost {
  opacity: 0.5;
  background: #c8ebfb !important;
}

.manual-table-ghost {
  opacity: 0.5;
  background: var(--color-primary-light) !important;
  border: 1px dashed var(--color-primary) !important;
}

.manual-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;

  &__stats {
    font-size: 13px;
    color: #595959;
  }

  &__actions {
    display: flex;
    gap: 8px;
  }
}
</style>
