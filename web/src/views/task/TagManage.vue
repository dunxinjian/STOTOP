<template>
  <div class="tag-manage">
    <div class="tag-manage__header">
      <h2>标签管理</h2>
      <a-button type="primary" @click="handleCreate">
        <PlusOutlined /> 新建标签
      </a-button>
    </div>

    <!-- 标签卡片列表 -->
    <a-spin :spinning="loading">
      <div v-if="tags.length" class="tag-manage__grid">
        <div v-for="tag in tags" :key="tag.id" class="tag-card">
          <div class="tag-card__header">
            <a-tag :color="tag.color" class="tag-card__badge">{{ tag.name }}</a-tag>
            <span class="tag-card__count">{{ tag.taskCount }} 个任务</span>
          </div>
          <div class="tag-card__info">
            <span class="tag-card__color">
              颜色: <span class="tag-card__color-dot" :style="{ backgroundColor: tag.color }"></span>
              {{ tag.color }}
            </span>
            <span class="tag-card__sort">排序: {{ tag.sort }}</span>
          </div>
          <div class="tag-card__actions">
            <a-button type="link" size="small" @click="handleEdit(tag)">
              <EditOutlined /> 编辑
            </a-button>
            <a-popconfirm title="确定删除此标签？" @confirm="handleDelete(tag.id)">
              <a-button type="link" size="small" danger>
                <DeleteOutlined /> 删除
              </a-button>
            </a-popconfirm>
          </div>
        </div>
      </div>
      <EmptyState v-else title="暂无标签" />
    </a-spin>

    <!-- 新建/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="isEdit ? '编辑标签' : '新建标签'"
      :confirm-loading="submitLoading"
      @ok="handleSubmit"
      width="420px"
    >
      <a-form :label-col="{ span: 5 }" :wrapper-col="{ span: 18 }">
        <a-form-item label="标签名称" required>
          <a-input v-model:value="form.name" placeholder="输入标签名称" :maxlength="20" />
        </a-form-item>
        <a-form-item label="标签颜色" required>
          <div class="color-picker">
            <div
              v-for="c in presetColors"
              :key="c"
              class="color-picker__item"
              :class="{ active: form.color === c }"
              :style="{ backgroundColor: c }"
              @click="form.color = c"
            />
            <a-input v-model:value="form.color" placeholder="#1890ff" style="width: 120px; margin-left: 8px" />
          </div>
        </a-form-item>
        <a-form-item label="排序">
          <a-input-number v-model:value="form.sort" :min="0" :max="999" style="width: 120px" />
        </a-form-item>
      </a-form>

      <!-- 预览 -->
      <div class="tag-preview">
        预览: <a-tag :color="form.color || 'default'">{{ form.name || '标签名称' }}</a-tag>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import { getTags, createTag, updateTag, deleteTag } from '@/api/task'
import type { TagListDto } from '@/types/task'

const loading = ref(false)
const tags = ref<TagListDto[]>([])

const presetColors = [
  '#1890ff', '#52c41a', '#faad14', '#f5222d', '#722ed1',
  '#13c2c2', '#eb2f96', '#fa8c16', '#a0d911', '#2f54eb',
]

async function loadData() {
  loading.value = true
  try {
    tags.value = await getTags()
  } catch {
    message.error('获取标签列表失败')
  } finally {
    loading.value = false
  }
}

// ---------- 新建/编辑 ----------
const modalVisible = ref(false)
const submitLoading = ref(false)
const isEdit = ref(false)
const editId = ref(0)

const form = reactive({
  name: '',
  color: '#1890ff',
  sort: 0,
})

function resetForm() {
  form.name = ''
  form.color = '#1890ff'
  form.sort = 0
}

function handleCreate() {
  isEdit.value = false
  editId.value = 0
  resetForm()
  modalVisible.value = true
}

function handleEdit(tag: TagListDto) {
  isEdit.value = true
  editId.value = tag.id
  form.name = tag.name
  form.color = tag.color
  form.sort = tag.sort
  modalVisible.value = true
}

async function handleSubmit() {
  if (!form.name.trim()) return message.warning('请输入标签名称')
  if (!form.color.trim()) return message.warning('请选择标签颜色')
  submitLoading.value = true
  try {
    if (isEdit.value) {
      await updateTag(editId.value, {
        name: form.name.trim(),
        color: form.color,
        sort: form.sort,
      })
      message.success('更新成功')
    } else {
      await createTag({
        name: form.name.trim(),
        color: form.color,
        sort: form.sort,
      })
      message.success('创建成功')
    }
    modalVisible.value = false
    loadData()
  } catch {
    message.error(isEdit.value ? '更新失败' : '创建失败')
  } finally {
    submitLoading.value = false
  }
}

async function handleDelete(id: number) {
  try {
    await deleteTag(id)
    message.success('删除成功')
    loadData()
  } catch {
    message.error('删除失败')
  }
}

onMounted(loadData)
</script>

<style scoped lang="scss">
.tag-manage {
  padding: 24px;

  &__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 20px;

    h2 {
      margin: 0;
      font-size: 20px;
      font-weight: 600;
    }
  }

  &__grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
    gap: 16px;
  }
}

.tag-card {
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 16px;
  transition: all 0.2s;

  &:hover {
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
    border-color: #d9d9d9;
  }

  &__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 10px;
  }

  &__badge {
    font-size: 14px;
  }

  &__count {
    font-size: 12px;
    color: #8c8c8c;
  }

  &__info {
    display: flex;
    gap: 16px;
    font-size: 13px;
    color: #595959;
    margin-bottom: 10px;
  }

  &__color {
    display: flex;
    align-items: center;
    gap: 4px;
  }

  &__color-dot {
    display: inline-block;
    width: 12px;
    height: 12px;
    border-radius: 2px;
  }

  &__actions {
    display: flex;
    gap: 4px;
    border-top: 1px solid #f0f0f0;
    padding-top: 8px;
  }
}

.color-picker {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;

  &__item {
    width: 24px;
    height: 24px;
    border-radius: 4px;
    cursor: pointer;
    border: 2px solid transparent;
    transition: all 0.2s;

    &:hover {
      transform: scale(1.15);
    }

    &.active {
      border-color: #262626;
      transform: scale(1.15);
    }
  }
}

.tag-preview {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid #f0f0f0;
  font-size: 13px;
  color: #8c8c8c;
}
</style>
