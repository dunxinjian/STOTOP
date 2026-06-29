<template>
  <div class="attendee-panel">
    <!-- 工具栏 -->
    <div class="toolbar" style="margin-bottom: 16px; display: flex; gap: 8px; align-items: center;">
      <a-button type="primary" @click="showAddDrawer"><PlusOutlined />新增人员</a-button>
      <a-button @click="handleImport"><ImportOutlined />导入Excel</a-button>
      <a-button @click="handleDownloadTemplate"><FileExcelOutlined />下载模板</a-button>
      <a-button @click="handleExport"><DownloadOutlined />导出名册</a-button>
      <a-divider type="vertical" />
      <a-select v-model:value="filters.role" placeholder="角色筛选" allowClear style="width:120px" :options="roleOptions" @change="handleFilterChange" />
      <a-select v-model:value="filters.status" placeholder="状态筛选" allowClear style="width:120px" :options="statusOptions" @change="handleFilterChange" />
      <a-select v-if="isWedding" v-model:value="filters.camp" placeholder="阵营筛选" allowClear style="width:120px" :options="campFilterOptions" @change="handleFilterChange" />
      <a-select v-model:value="filters.checkInStatus" placeholder="签到状态" allowClear style="width:120px" @change="handleFilterChange">
        <a-select-option value="未签到">未签到</a-select-option>
        <a-select-option value="已到场">已到场</a-select-option>
        <a-select-option value="未到场">未到场</a-select-option>
      </a-select>
      <a-select
        v-model:value="hasClearTravelDateProxy"
        placeholder="来往日期"
        allowClear
        style="width: 120px"
        @change="handleFilterChange"
      >
        <a-select-option :value="0">未明确</a-select-option>
        <a-select-option :value="1">已明确</a-select-option>
      </a-select>
      <a-input-search v-model:value="filters.keyword" placeholder="姓名/单位/电话/随行人员" style="width:200px" @search="handleFilterChange" />
    </div>

    <!-- 批量操作栏 -->
    <div v-if="selectedRowKeys.length > 0" style="margin-bottom: 12px; display: flex; gap: 8px; align-items: center; background: var(--bg-muted); padding: 8px 16px; border-radius: 4px;">
      <span>已选中 <b>{{ selectedRowKeys.length }}</b> 人</span>
      <a-divider type="vertical" />
      <a-dropdown>
        <a-button size="small">批量更新签到状态 <DownOutlined /></a-button>
        <template #overlay>
          <a-menu @click="({ key }: any) => handleBatchUpdateStatus(key as string)">
            <a-menu-item key="未签到">未签到</a-menu-item>
            <a-menu-item key="已到场">已到场</a-menu-item>
            <a-menu-item key="未到场">未到场</a-menu-item>
          </a-menu>
        </template>
      </a-dropdown>
      <a-dropdown>
        <a-button size="small">批量更新状态 <DownOutlined /></a-button>
        <template #overlay>
          <a-menu @click="({ key }: any) => handleBatchUpdateStatus(undefined, key as string)">
            <a-menu-item key="待确认">待确认</a-menu-item>
            <a-menu-item key="已确认">已确认</a-menu-item>
            <a-menu-item key="已取消">已取消</a-menu-item>
          </a-menu>
        </template>
      </a-dropdown>
      <a-button size="small" type="primary" @click="handleBatchUpdateStatus('已到场')">全部标记已到场</a-button>
      <a-button size="small" @click="selectedRowKeys = []">取消选择</a-button>
    </div>

    <!-- 表格 -->
    <a-table
      :columns="tableColumns"
      :data-source="filteredDataSource"
      :loading="loading"
      :scroll="{ x: isWedding ? 2100 : 1700 }"
      row-key="id"
      :pagination="pagination"
      @change="handleTableChange"
      :expandedRowKeys="expandedRowKeys"
      @expandedRowsChange="(keys: Key[]) => expandedRowKeys = keys.map(Number)"
      :row-selection="{ selectedRowKeys, onChange: (keys: Key[]) => selectedRowKeys = keys.map(Number) }"
    >
      <template #expandedRowRender="{ record }" v-if="isWedding">
        <div v-if="record.companions && record.companions.length > 0" style="margin: 0;">
          <a-table
            :columns="companionColumns"
            :data-source="record.companions"
            :pagination="false"
            row-key="id"
            size="small"
          >
            <template #bodyCell="{ column, record: comp }">
              <template v-if="column.dataIndex === 'hasSeat'">
                <a-tag :color="comp.hasSeat ? 'green' : 'default'">{{ comp.hasSeat ? '占座' : '不占座' }}</a-tag>
              </template>
              <template v-else-if="column.dataIndex === 'mealCategory'">
                <a-tag>{{ comp.mealCategory || '-' }}</a-tag>
              </template>
              <template v-else-if="column.key === 'action'">
                <a-space>
                  <a @click="handleEditCompanionFromList(record as AttendeeListItemDto, comp as CompanionDto)">编辑</a>
                  <a style="color: var(--color-danger)" @click="handleDeleteCompanionFromList(record as AttendeeListItemDto, comp as CompanionDto)">删除</a>
                </a-space>
              </template>
            </template>
          </a-table>
        </div>
        <a-empty v-else description="暂无随行人员" :image="null" style="margin: 8px 0;">
          <template #image><span></span></template>
        </a-empty>
      </template>
      <template #bodyCell="{ column, record, index }">
        <template v-if="column.key === 'index'">
          {{ (pagination.current - 1) * pagination.pageSize + index + 1 }}
        </template>
        <template v-else-if="column.key === 'name'">
          <a @click="showDetail(record as AttendeeListItemDto)" :style="{ color: getStatusColor(record.status) }">{{ record.name }}</a>
        </template>
        <template v-else-if="column.dataIndex === 'role'">
          <a-tag :color="roleColorMap[record.role]">{{ record.role || '-' }}</a-tag>
        </template>
        <template v-else-if="column.dataIndex === 'camp'">
          <a-tag :color="campColorMap[record.camp]">{{ record.camp || '-' }}</a-tag>
        </template>
        <template v-else-if="column.dataIndex === 'guestType'">
          {{ record.guestType || '-' }}
        </template>
        <template v-else-if="column.dataIndex === 'companionCount'">
          <a-badge :count="record.companionCount || 0" :number-style="{ backgroundColor: record.companionCount > 0 ? 'var(--color-info)' : '#d9d9d9' }" />
        </template>
        <template v-else-if="column.dataIndex === 'needPickup'">
          <span :style="{ color: record.needPickup ? 'var(--color-success)' : '#d9d9d9', fontSize: '16px' }">●</span>
        </template>
        <template v-else-if="column.dataIndex === 'needAccommodation'">
          <span :style="{ color: record.needAccommodation ? 'var(--color-success)' : '#d9d9d9', fontSize: '16px' }">●</span>
        </template>
        <template v-else-if="column.dataIndex === 'arrivalTime'">
          {{ record.arrivalTime ? formatDateTime(record.arrivalTime) : '-' }}
        </template>
        <template v-else-if="column.dataIndex === 'departureTime'">
          {{ record.departureTime ? formatDateTime(record.departureTime) : '-' }}
        </template>
        <template v-else-if="column.dataIndex === 'checkInStatus'">
          <a-tag :color="checkInStatusColorMap[record.checkInStatus] || 'default'">{{ record.checkInStatus || '未签到' }}</a-tag>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a @click="showEditDrawer(record as AttendeeListItemDto)">编辑</a>
            <a-dropdown>
              <a>签到 <DownOutlined /></a>
              <template #overlay>
                <a-menu @click="({ key }: any) => quickUpdateStatus(record.id, key as string)">
                  <a-menu-item key="未签到">未签到</a-menu-item>
                  <a-menu-item key="已到场">已到场</a-menu-item>
                  <a-menu-item key="未到场">未到场</a-menu-item>
                </a-menu>
              </template>
            </a-dropdown>
            <a-dropdown>
              <a>状态 <DownOutlined /></a>
              <template #overlay>
                <a-menu @click="({ key }: any) => quickUpdateStatus(record.id, undefined, key as string)">
                  <a-menu-item key="待确认">待确认</a-menu-item>
                  <a-menu-item key="已确认">已确认</a-menu-item>
                  <a-menu-item key="已取消">已取消</a-menu-item>
                </a-menu>
              </template>
            </a-dropdown>
            <a-popconfirm title="确定删除该人员？" @confirm="handleDelete(record.id)">
              <a style="color: var(--color-danger)">删除</a>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 新增/编辑抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="isEdit ? '编辑人员' : '新增人员'"
      :width="720"
      :destroyOnClose="true"
      @close="resetForm"
    >
      <a-steps :current="currentStep" style="margin-bottom: 24px">
        <a-step title="基本信息" />
        <a-step title="行程信息" />
      </a-steps>

      <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
        <!-- Step 1: 基本信息 -->
        <div v-show="currentStep === 0">
          <a-form-item label="姓名" required>
            <a-input v-model:value="form.name" placeholder="请输入姓名" />
          </a-form-item>
          <a-form-item label="性别">
            <a-select v-model:value="form.gender" placeholder="请选择" allowClear>
              <a-select-option value="男">男</a-select-option>
              <a-select-option value="女">女</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="单位">
            <a-input v-model:value="form.organization" placeholder="请输入单位" />
          </a-form-item>
          <a-form-item label="职务">
            <a-input v-model:value="form.title" placeholder="请输入职务" />
          </a-form-item>
          <a-form-item label="角色">
            <a-select v-model:value="form.role" placeholder="请选择角色" allowClear :options="roleOptions" />
          </a-form-item>
          <!-- 婚礼模式新增字段 -->
          <a-form-item v-if="isWedding" label="阵营">
            <a-select v-model:value="form.camp" placeholder="请选择阵营" allowClear :options="campOptions" />
          </a-form-item>
          <a-form-item v-if="isWedding" label="宾客类型">
            <a-select v-model:value="form.guestType" placeholder="请选择宾客类型" allowClear :options="guestTypeOptions" />
          </a-form-item>
          <a-form-item label="电话">
            <a-input v-model:value="form.phone" placeholder="请输入电话" />
          </a-form-item>
          <a-form-item label="邮箱">
            <a-input v-model:value="form.email" placeholder="请输入邮箱" />
          </a-form-item>
          <a-form-item label="身份证号">
            <a-input v-model:value="form.idCard" placeholder="请输入身份证号" />
          </a-form-item>
          <a-form-item label="状态" v-if="isEdit">
            <a-select v-model:value="form.status" placeholder="请选择状态">
              <a-select-option value="待确认">待确认</a-select-option>
              <a-select-option value="已确认">已确认</a-select-option>
              <a-select-option value="已取消">已取消</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="备注">
            <a-textarea v-model:value="form.remark" placeholder="请输入备注" :rows="3" />
          </a-form-item>

          <!-- 婚礼模式：随行人员区域 -->
          <template v-if="isWedding">
            <a-divider orientation="left">随行人员</a-divider>
            <div style="margin-bottom: 16px;">
              <a-table
                v-if="companionList.length > 0"
                :columns="companionEditColumns"
                :data-source="companionList"
                :pagination="false"
                row-key="id"
                size="small"
                style="margin-bottom: 12px;"
              >
                <template #bodyCell="{ column, record: comp }">
                  <template v-if="column.dataIndex === 'hasSeat'">
                    <a-tag :color="comp.hasSeat ? 'green' : 'default'">{{ comp.hasSeat ? '占座' : '不占座' }}</a-tag>
                  </template>
                  <template v-else-if="column.dataIndex === 'mealCategory'">
                    <a-tag>{{ comp.mealCategory || '-' }}</a-tag>
                  </template>
                  <template v-else-if="column.key === 'action'">
                    <a-space>
                      <a @click="showEditCompanionModal(comp as CompanionDto)">编辑</a>
                      <a style="color: var(--color-danger)" @click="handleDeleteCompanion(comp as CompanionDto)">删除</a>
                    </a-space>
                  </template>
                </template>
              </a-table>
              <a-empty v-else description="暂无随行人员" style="margin: 8px 0;" />
              <a-button type="dashed" block @click="showCompanionModal" :disabled="!isEdit && !editId">
                <PlusOutlined /> 添加随行人员
              </a-button>
              <div v-if="!isEdit" style="color: #999; font-size: 12px; margin-top: 4px;">
                请先保存主宾客后再添加随行人员
              </div>
            </div>
          </template>
        </div>

        <!-- Step 2: 行程信息 -->
        <div v-show="currentStep === 1">
          <a-divider orientation="left">来程</a-divider>
          <a-form-item label="是否需要接送">
            <a-switch v-model:checked="form.needPickup" />
          </a-form-item>
          <a-form-item label="到达方式">
            <a-select v-model:value="form.arrivalMode" placeholder="请选择" allowClear>
              <a-select-option value="飞机">飞机</a-select-option>
              <a-select-option value="火车">火车</a-select-option>
              <a-select-option value="自驾">自驾</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="航班/车次">
            <a-input v-model:value="form.arrivalFlightTrain" placeholder="请输入航班或车次号" />
          </a-form-item>
          <a-form-item label="到达时间">
            <a-date-picker v-model:value="form.arrivalTime" show-time format="YYYY-MM-DD HH:mm" value-format="YYYY-MM-DDTHH:mm:ss" style="width: 100%" />
          </a-form-item>
          <a-form-item label="到达站点">
            <a-input v-model:value="form.arrivalStation" placeholder="请输入到达站点" />
          </a-form-item>

          <a-divider orientation="left">回程</a-divider>
          <a-form-item label="离开方式">
            <a-select v-model:value="form.departureMode" placeholder="请选择" allowClear>
              <a-select-option value="飞机">飞机</a-select-option>
              <a-select-option value="火车">火车</a-select-option>
              <a-select-option value="自驾">自驾</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="航班/车次">
            <a-input v-model:value="form.departureFlightTrain" placeholder="请输入航班或车次号" />
          </a-form-item>
          <a-form-item label="离开时间">
            <a-date-picker v-model:value="form.departureTime" show-time format="YYYY-MM-DD HH:mm" value-format="YYYY-MM-DDTHH:mm:ss" style="width: 100%" />
          </a-form-item>
          <a-form-item label="离开站点">
            <a-input v-model:value="form.departureStation" placeholder="请输入离开站点" />
          </a-form-item>

          <a-divider orientation="left">住宿</a-divider>
          <a-form-item label="是否需要住宿">
            <a-switch v-model:checked="form.needAccommodation" />
          </a-form-item>
          <a-form-item label="房型偏好">
            <a-select v-model:value="form.preferredRoomType" placeholder="请选择房型偏好" allowClear>
              <a-select-option value="标单">标单</a-select-option>
              <a-select-option value="标双">标双</a-select-option>
              <a-select-option value="套房">套房</a-select-option>
              <a-select-option value="大床房">大床房</a-select-option>
              <a-select-option value="行政大床">行政大床</a-select-option>
              <a-select-option value="其他">其他</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="同住偏好">
            <a-input v-model:value="form.roommatePreference" placeholder="请输入同住偏好" />
          </a-form-item>
          <a-form-item label="饮食禁忌">
            <a-textarea v-model:value="form.dietPreference" placeholder="请输入饮食禁忌" :rows="3" />
          </a-form-item>
        </div>
      </a-form>

      <template #footer>
        <div style="display: flex; justify-content: space-between">
          <div>
            <a-button v-if="currentStep > 0" @click="currentStep--">上一步</a-button>
          </div>
          <div style="display: flex; gap: 8px">
            <a-button @click="drawerVisible = false">取消</a-button>
            <a-button v-if="currentStep < 1" type="primary" @click="currentStep++">下一步</a-button>
            <a-button v-else type="primary" :loading="submitting" @click="handleSubmit">保存</a-button>
          </div>
        </div>
      </template>
    </a-drawer>

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="detailVisible"
      title="人员详情"
      :width="600"
      :destroyOnClose="true"
    >
      <a-descriptions :column="2" bordered size="small" v-if="detailData">
        <a-descriptions-item label="姓名">{{ detailData.name }}</a-descriptions-item>
        <a-descriptions-item label="性别">{{ detailData.gender || '-' }}</a-descriptions-item>
        <a-descriptions-item label="单位">{{ detailData.organization || '-' }}</a-descriptions-item>
        <a-descriptions-item label="职务">{{ detailData.title || '-' }}</a-descriptions-item>
        <a-descriptions-item label="角色">
          <a-tag :color="roleColorMap[detailData.role ?? '']">{{ detailData.role || '-' }}</a-tag>
        </a-descriptions-item>
        <a-descriptions-item label="电话">{{ detailData.phone || '-' }}</a-descriptions-item>
        <template v-if="isWedding">
          <a-descriptions-item label="阵营">
            <a-tag :color="campColorMap[detailData.camp ?? '']">{{ detailData.camp || '-' }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="宾客类型">{{ detailData.guestType || '-' }}</a-descriptions-item>
        </template>
        <a-descriptions-item label="状态">{{ detailData.status }}</a-descriptions-item>
        <a-descriptions-item label="需要接送">{{ detailData.needPickup ? '是' : '否' }}</a-descriptions-item>
        <a-descriptions-item label="需要住宿">{{ detailData.needAccommodation ? '是' : '否' }}</a-descriptions-item>
        <a-descriptions-item label="到达方式">{{ detailData.arrivalMode || '-' }}</a-descriptions-item>
        <a-descriptions-item label="航班/车次">{{ detailData.arrivalFlightTrain || '-' }}</a-descriptions-item>
        <a-descriptions-item label="到达时间">{{ detailData.arrivalTime ? formatDateTime(detailData.arrivalTime) : '-' }}</a-descriptions-item>
        <a-descriptions-item label="到达站点">{{ detailData.arrivalStation || '-' }}</a-descriptions-item>
        <a-descriptions-item label="离开方式">{{ detailData.departureMode || '-' }}</a-descriptions-item>
        <a-descriptions-item label="航班/车次">{{ detailData.departureFlightTrain || '-' }}</a-descriptions-item>
        <a-descriptions-item label="离开时间">{{ detailData.departureTime ? formatDateTime(detailData.departureTime) : '-' }}</a-descriptions-item>
        <a-descriptions-item label="离开站点">{{ detailData.departureStation || '-' }}</a-descriptions-item>
        <a-descriptions-item label="饮食禁忌" :span="2">{{ detailData.dietPreference || '-' }}</a-descriptions-item>
        <a-descriptions-item label="备注" :span="2">{{ detailData.remark || '-' }}</a-descriptions-item>
      </a-descriptions>

      <!-- 婚礼模式：详情中展示随行人员 -->
      <template v-if="isWedding && detailData">
        <a-divider orientation="left">随行人员 ({{ detailData.companionCount || 0 }}人)</a-divider>
        <a-table
          v-if="detailData.companions && detailData.companions.length > 0"
          :columns="companionColumns"
          :data-source="detailData.companions"
          :pagination="false"
          row-key="id"
          size="small"
        >
          <template #bodyCell="{ column, record: comp }">
            <template v-if="column.dataIndex === 'hasSeat'">
              <a-tag :color="comp.hasSeat ? 'green' : 'default'">{{ comp.hasSeat ? '占座' : '不占座' }}</a-tag>
            </template>
            <template v-else-if="column.dataIndex === 'mealCategory'">
              <a-tag>{{ comp.mealCategory || '-' }}</a-tag>
            </template>
          </template>
        </a-table>
        <EmptyState v-else size="small" title="暂无随行人员" />
      </template>
    </a-drawer>

    <!-- 添加/编辑随行人员 Modal -->
    <a-modal
      v-model:open="companionModalVisible"
      :title="editingCompanionId ? '编辑随行人员' : '添加随行人员'"
      :width="480"
      @ok="handleSaveCompanion"
      :confirmLoading="companionSubmitting"
      @cancel="resetCompanionForm"
    >
      <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
        <a-form-item label="姓名" required>
          <a-input v-model:value="companionForm.name" placeholder="请输入姓名" />
        </a-form-item>
        <a-form-item label="关系">
          <a-select v-model:value="companionForm.relation" placeholder="请选择关系" allowClear :options="relationOptions" />
        </a-form-item>
        <a-form-item label="年龄">
          <a-input-number v-model:value="companionForm.age" placeholder="请输入年龄" :min="0" :max="150" style="width: 100%" />
        </a-form-item>
        <a-form-item label="占座">
          <a-tag :color="computedSeatInfo.hasSeat ? 'green' : 'default'">{{ computedSeatInfo.hasSeat ? '占座' : '不占座' }}</a-tag>
          <span style="margin-left: 8px; color: #999; font-size: 12px;">（根据年龄自动推算）</span>
        </a-form-item>
        <a-form-item label="餐标">
          <a-tag>{{ computedSeatInfo.mealCategory }}</a-tag>
          <span style="margin-left: 8px; color: #999; font-size: 12px;">（根据年龄自动推算）</span>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { message, Modal } from 'ant-design-vue'
import { PlusOutlined, ImportOutlined, DownloadOutlined, FileExcelOutlined, DownOutlined } from '@ant-design/icons-vue'
import {
  getAttendees, getAttendee, createAttendee, updateAttendee, deleteAttendee,
  importAttendees, exportAttendees, downloadAttendeeTemplate,
  createCompanion, getCompanions, updateCompanion, deleteCompanion,
  batchUpdateAttendeeStatus
} from '@/api/conference'
import type { AttendeeListItemDto, AttendeeDto, CreateAttendeeRequest, UpdateAttendeeRequest, CompanionDto } from '@/api/conference'
import type { Key } from 'ant-design-vue/es/table/interface'
import dayjs from 'dayjs'

const props = defineProps<{ eventId: number; eventData?: any }>()

// ==================== 计算属性 ====================

const isWedding = computed(() => props.eventData?.type === 'wedding')

// ==================== 常量 ====================

const roleOptions = [
  { label: '嘉宾', value: '嘉宾' },
  { label: '领导', value: '领导' },
  { label: '参会代表', value: '参会代表' },
  { label: '工作人员', value: '工作人员' },
  { label: '媒体', value: '媒体' },
]

const statusOptions = [
  { label: '已确认', value: '已确认' },
  { label: '待确认', value: '待确认' },
  { label: '已取消', value: '已取消' },
]

const campOptions = [
  { label: '男方', value: '男方' },
  { label: '女方', value: '女方' },
  { label: '共同', value: '共同' },
]

const campFilterOptions = [
  { label: '全部', value: '' },
  { label: '男方', value: '男方' },
  { label: '女方', value: '女方' },
  { label: '共同', value: '共同' },
]

const guestTypeOptions = [
  { label: '亲属', value: '亲属' },
  { label: '同学', value: '同学' },
  { label: '同事', value: '同事' },
  { label: '领导', value: '领导' },
  { label: '朋友', value: '朋友' },
  { label: '世交', value: '世交' },
]

const relationOptions = [
  { label: '配偶', value: '配偶' },
  { label: '子女', value: '子女' },
  { label: '父母', value: '父母' },
  { label: '兄弟姐妹', value: '兄弟姐妹' },
  { label: '其他', value: '其他' },
]

const roleColorMap: Record<string, string> = {
  '嘉宾': 'gold',
  '领导': 'red',
  '参会代表': 'blue',
  '工作人员': 'green',
  '媒体': 'purple',
}

const campColorMap: Record<string, string> = {
  '男方': 'blue',
  '女方': 'pink',
  '共同': 'green',
}

const checkInStatusColorMap: Record<string, string> = {
  '未签到': 'default',
  '已到场': 'success',
  '未到场': 'error',
}

const baseColumns = [
  { title: '序号', key: 'index', width: 60, fixed: 'left' as const },
  { title: '姓名', dataIndex: 'name', key: 'name', width: 100, fixed: 'left' as const },
  { title: '单位', dataIndex: 'organization', width: 150 },
  { title: '职务', dataIndex: 'title', width: 100 },
  { title: '角色', dataIndex: 'role', width: 100 },
  { title: '性别', dataIndex: 'gender', width: 70 },
  { title: '电话', dataIndex: 'phone', width: 120 },
  { title: '接送', dataIndex: 'needPickup', width: 70, align: 'center' as const },
  { title: '住宿', dataIndex: 'needAccommodation', width: 70, align: 'center' as const },
  { title: '房型偏好', dataIndex: 'preferredRoomType', width: 100 },
  { title: '来程', dataIndex: 'arrivalTime', width: 130 },
  { title: '回程', dataIndex: 'departureTime', width: 130 },
  { title: '签到状态', dataIndex: 'checkInStatus', width: 100 },
  { title: '操作', key: 'action', width: 220, fixed: 'right' as const },
]

const weddingExtraColumns = [
  { title: '阵营', dataIndex: 'camp', width: 80 },
  { title: '宾客类型', dataIndex: 'guestType', width: 100 },
  { title: '随行人数', dataIndex: 'companionCount', width: 90, align: 'center' as const },
]

const tableColumns = computed(() => {
  if (!isWedding.value) return baseColumns
  const cols = [...baseColumns]
  // 在"角色"列后面插入婚礼列
  const roleIdx = cols.findIndex(c => c.dataIndex === 'role')
  cols.splice(roleIdx + 1, 0, ...weddingExtraColumns)
  return cols
})

const companionColumns = [
  { title: '姓名', dataIndex: 'name', width: 100 },
  { title: '关系', dataIndex: 'relation', width: 80 },
  { title: '年龄', dataIndex: 'age', width: 60 },
  { title: '占座', dataIndex: 'hasSeat', width: 80 },
  { title: '餐标', dataIndex: 'mealCategory', width: 80 },
  { title: '操作', key: 'action', width: 120 },
]

const companionEditColumns = [
  { title: '姓名', dataIndex: 'name', width: 100 },
  { title: '关系', dataIndex: 'relation', width: 80 },
  { title: '年龄', dataIndex: 'age', width: 60 },
  { title: '占座', dataIndex: 'hasSeat', width: 80 },
  { title: '餐标', dataIndex: 'mealCategory', width: 80 },
  { title: '操作', key: 'action', width: 120 },
]

// ==================== 状态 ====================

const loading = ref(false)
const submitting = ref(false)
const dataSource = ref<AttendeeListItemDto[]>([])
const expandedRowKeys = ref<number[]>([])
const selectedRowKeys = ref<number[]>([])
const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})
const filters = reactive({
  role: undefined as string | undefined,
  status: undefined as string | undefined,
  camp: undefined as string | undefined,
  checkInStatus: undefined as string | undefined,
  hasClearTravelDate: undefined as boolean | undefined,
  keyword: '',
})

// 来往日期筛选：底层保留 boolean，下拉用 1/0 代理（AntD SelectValue 仅接受 string|number）
const hasClearTravelDateProxy = computed<number | undefined>({
  get: () => (filters.hasClearTravelDate === undefined ? undefined : filters.hasClearTravelDate ? 1 : 0),
  set: (v) => {
    filters.hasClearTravelDate = v === undefined ? undefined : v === 1
  },
})

// 阵营筛选已传递给后端，直接使用 dataSource
const filteredDataSource = computed(() => dataSource.value)

// 抽屉
const drawerVisible = ref(false)
const isEdit = ref(false)
const editId = ref<number | null>(null)
const currentStep = ref(0)

const form = reactive({
  name: '',
  gender: undefined as string | undefined,
  organization: '',
  title: '',
  role: undefined as string | undefined,
  camp: undefined as string | undefined,
  guestType: undefined as string | undefined,
  phone: '',
  email: '',
  idCard: '',
  remark: '',
  needPickup: false,
  arrivalMode: undefined as string | undefined,
  arrivalFlightTrain: '',
  arrivalTime: undefined as string | undefined,
  arrivalStation: '',
  departureMode: undefined as string | undefined,
  departureFlightTrain: '',
  departureTime: undefined as string | undefined,
  departureStation: '',
  needAccommodation: false,
  preferredRoomType: undefined as string | undefined,
  roommatePreference: '',
  dietPreference: '',
  status: undefined as string | undefined,
})

// 详情
const detailVisible = ref(false)
const detailData = ref<AttendeeDto | null>(null)

// 随行人员
const companionList = ref<CompanionDto[]>([])
const companionModalVisible = ref(false)
const companionSubmitting = ref(false)
const editingCompanionId = ref<number | null>(null)
const companionForm = reactive({
  name: '',
  relation: undefined as string | undefined,
  age: undefined as number | undefined,
})

// 根据年龄自动推算占座和餐标
const computedSeatInfo = computed(() => {
  const age = companionForm.age
  if (age === undefined || age === null || age >= 12) {
    return { hasSeat: true, mealCategory: '全餐' }
  } else if (age >= 6) {
    return { hasSeat: true, mealCategory: '儿童餐' }
  } else if (age >= 3) {
    return { hasSeat: false, mealCategory: '不用餐' }
  } else {
    return { hasSeat: false, mealCategory: '不用餐' }
  }
})

// ==================== 方法 ====================

function getStatusColor(status: string): string {
  switch (status) {
    case '已确认': return 'var(--color-success)'
    case '已取消': return '#999999'
    case '待确认':
    default: return 'var(--color-warning)'
  }
}

function formatDateTime(dt: string) {
  return dayjs(dt).format('MM-DD HH:mm')
}

async function loadAttendees() {
  loading.value = true
  try {
    const res: any = await getAttendees(props.eventId, {
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
      keyword: filters.keyword || undefined,
      role: filters.role,
      status: filters.status,
      camp: filters.camp || undefined,
      checkInStatus: filters.checkInStatus,
      hasClearTravelDate: filters.hasClearTravelDate,
    })
    dataSource.value = res.items || res || []
    pagination.total = res.total ?? dataSource.value.length
  } catch (err: any) {
    message.error('加载人员列表失败')
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: any) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  loadAttendees()
}

function handleFilterChange() {
  pagination.current = 1
  loadAttendees()
}

function showAddDrawer() {
  isEdit.value = false
  editId.value = null
  currentStep.value = 0
  companionList.value = []
  resetFormFields()
  drawerVisible.value = true
}

async function showEditDrawer(record: AttendeeListItemDto) {
  isEdit.value = true
  editId.value = record.id
  currentStep.value = 0
  companionList.value = []
  try {
    const detail: any = await getAttendee(record.id)
    Object.assign(form, {
      name: detail.name || '',
      gender: detail.gender || undefined,
      organization: detail.organization || '',
      title: detail.title || '',
      role: detail.role || undefined,
      camp: detail.camp || undefined,
      guestType: detail.guestType || undefined,
      phone: detail.phone || '',
      email: '',
      idCard: '',
      remark: detail.remark || '',
      needPickup: detail.needPickup ?? false,
      arrivalMode: detail.arrivalMode || undefined,
      arrivalFlightTrain: detail.arrivalFlightTrain || '',
      arrivalTime: detail.arrivalTime || undefined,
      arrivalStation: detail.arrivalStation || '',
      departureMode: detail.departureMode || undefined,
      departureFlightTrain: detail.departureFlightTrain || '',
      departureTime: detail.departureTime || undefined,
      departureStation: detail.departureStation || '',
      needAccommodation: detail.needAccommodation ?? false,
      preferredRoomType: detail.preferredRoomType || undefined,
      roommatePreference: '',
      dietPreference: detail.dietPreference || '',
      status: detail.status || undefined,
    })
    // 婚礼模式下加载随行人员
    if (isWedding.value) {
      await loadCompanions(record.id)
    }
    drawerVisible.value = true
  } catch {
    message.error('加载人员详情失败')
  }
}

async function loadCompanions(attendeeId: number) {
  try {
    const res: any = await getCompanions(attendeeId)
    companionList.value = res || []
  } catch {
    companionList.value = []
  }
}

async function showDetail(record: AttendeeListItemDto) {
  try {
    detailData.value = await getAttendee(record.id) as any
    detailVisible.value = true
  } catch {
    message.error('加载详情失败')
  }
}

function resetFormFields() {
  Object.assign(form, {
    name: '', gender: undefined, organization: '', title: '',
    role: undefined, camp: undefined, guestType: undefined,
    phone: '', email: '', idCard: '', remark: '',
    status: undefined,
    needPickup: false, arrivalMode: undefined, arrivalFlightTrain: '',
    arrivalTime: undefined, arrivalStation: '',
    departureMode: undefined, departureFlightTrain: '',
    departureTime: undefined, departureStation: '',
    needAccommodation: false, preferredRoomType: undefined, roommatePreference: '',
    dietPreference: '',
  })
}

function resetForm() {
  resetFormFields()
  currentStep.value = 0
  companionList.value = []
}

async function handleSubmit() {
  if (!form.name.trim()) {
    message.warning('请输入姓名')
    currentStep.value = 0
    return
  }
  submitting.value = true
  try {
    const payload: any = {
      name: form.name,
      gender: form.gender,
      phone: form.phone,
      organization: form.organization,
      title: form.title,
      role: form.role,
      dietPreference: form.dietPreference || undefined,
      arrivalMode: form.arrivalMode,
      arrivalFlightTrain: form.arrivalFlightTrain || undefined,
      arrivalTime: form.arrivalTime,
      arrivalStation: form.arrivalStation || undefined,
      departureMode: form.departureMode,
      departureFlightTrain: form.departureFlightTrain || undefined,
      departureTime: form.departureTime,
      departureStation: form.departureStation || undefined,
      needPickup: form.needPickup,
      needAccommodation: form.needAccommodation,
      preferredRoomType: form.preferredRoomType || undefined,
      remark: form.remark || undefined,
    }
    // 编辑时传递 status
    if (isEdit.value) {
      payload.status = form.status
    }
    // 婚礼模式额外字段
    if (isWedding.value) {
      payload.camp = form.camp
      payload.guestType = form.guestType
    }
    if (isEdit.value && editId.value) {
      await updateAttendee(editId.value, payload)
      message.success('更新成功')
    } else {
      await createAttendee(props.eventId, payload)
      message.success('新增成功')
    }
    drawerVisible.value = false
    loadAttendees()
  } catch (err: any) {
    message.error((isEdit.value ? '更新' : '新增') + '失败：' + (err.message || '未知错误'))
  } finally {
    submitting.value = false
  }
}

async function handleDelete(id: number) {
  try {
    await deleteAttendee(id)
    message.success('删除成功')
    loadAttendees()
  } catch {
    message.error('删除失败')
  }
}

// ==================== 随行人员 ====================

function showCompanionModal() {
  resetCompanionForm()
  editingCompanionId.value = null
  companionModalVisible.value = true
}

function showEditCompanionModal(comp: CompanionDto) {
  editingCompanionId.value = comp.id
  companionForm.name = comp.name || ''
  companionForm.relation = comp.relation || undefined
  companionForm.age = comp.age ?? undefined
  companionModalVisible.value = true
}

function handleEditCompanionFromList(record: AttendeeListItemDto, comp: CompanionDto) {
  // 从主表展开行编辑随行人员，需设置 editId 以便保存后刷新
  editId.value = record.id
  showEditCompanionModal(comp)
}

function handleDeleteCompanionFromList(record: AttendeeListItemDto, comp: CompanionDto) {
  editId.value = record.id
  handleDeleteCompanion(comp)
}

function resetCompanionForm() {
  companionForm.name = ''
  companionForm.relation = undefined
  companionForm.age = undefined
  editingCompanionId.value = null
}

async function handleSaveCompanion() {
  if (!companionForm.name.trim()) {
    message.warning('请输入随行人员姓名')
    return
  }
  if (!editId.value) {
    message.warning('请先保存主宾客')
    return
  }
  companionSubmitting.value = true
  try {
    const seatInfo = computedSeatInfo.value
    const payload = {
      name: companionForm.name,
      relation: companionForm.relation,
      age: companionForm.age,
      hasSeat: seatInfo.hasSeat,
      mealCategory: seatInfo.mealCategory,
    } as any
    if (editingCompanionId.value) {
      await updateCompanion(editingCompanionId.value, payload)
      message.success('随行人员更新成功')
    } else {
      await createCompanion(props.eventId, editId.value, payload)
      message.success('随行人员添加成功')
    }
    companionModalVisible.value = false
    resetCompanionForm()
    // 重新加载随行人员列表
    await loadCompanions(editId.value)
    // 刷新主列表以更新随行人数
    loadAttendees()
  } catch (err: any) {
    message.error((editingCompanionId.value ? '更新' : '添加') + '随行人员失败：' + (err.message || '未知错误'))
  } finally {
    companionSubmitting.value = false
  }
}

function handleDeleteCompanion(comp: CompanionDto) {
  Modal.confirm({
    title: '确认删除',
    content: `确定删除随行人员「${comp.name}」？`,
    okText: '删除',
    okType: 'danger',
    cancelText: '取消',
    async onOk() {
      try {
        await deleteCompanion(comp.id)
        message.success('删除随行人员成功')
        if (editId.value) {
          await loadCompanions(editId.value)
        }
        loadAttendees()
      } catch {
        message.error('删除随行人员失败')
      }
    },
  })
}

// ==================== 导入导出 ====================

async function handleBatchUpdateStatus(checkInStatus?: string, status?: string) {
  if (selectedRowKeys.value.length === 0) {
    message.warning('请先选择人员')
    return
  }
  try {
    await batchUpdateAttendeeStatus({
      attendeeIds: selectedRowKeys.value,
      checkInStatus,
      status,
    })
    message.success('状态更新成功')
    selectedRowKeys.value = []
    loadAttendees()
  } catch {
    message.error('状态更新失败')
  }
}

async function quickUpdateStatus(id: number, checkInStatus?: string, status?: string) {
  try {
    await batchUpdateAttendeeStatus({
      attendeeIds: [id],
      checkInStatus,
      status,
    })
    message.success('状态更新成功')
    loadAttendees()
  } catch {
    message.error('状态更新失败')
  }
}

// ==================== 导入导出操作 ====================

const handleImport = () => {
  const input = document.createElement('input')
  input.type = 'file'
  input.accept = '.xlsx,.xls,.csv'
  input.onchange = async (e: any) => {
    const file = e.target.files[0]
    if (!file) return
    try {
      const result: any = await importAttendees(props.eventId, file)
      message.success(`成功导入 ${result?.importCount ?? 0} 条记录`)
      loadAttendees()
    } catch (err: any) {
      message.error('导入失败：' + (err.message || '未知错误'))
    }
  }
  input.click()
}

const handleExport = async () => {
  try {
    const blob: any = await exportAttendees(props.eventId)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `参会人员名册_${props.eventData?.name || ''}.xlsx`
    a.click()
    URL.revokeObjectURL(url)
  } catch {
    message.error('导出失败')
  }
}

const handleDownloadTemplate = async () => {
  try {
    const blob: any = await downloadAttendeeTemplate()
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = '参会人员导入模板.xlsx'
    a.click()
    URL.revokeObjectURL(url)
  } catch {
    message.error('下载模板失败')
  }
}

// ==================== 生命周期 ====================

onMounted(() => {
  loadAttendees()
})

watch(() => props.eventId, () => {
  pagination.current = 1
  loadAttendees()
})
</script>

<style scoped>
.attendee-panel {
  padding: 0;
}

.toolbar {
  flex-wrap: wrap;
}
</style>
