<template>
  <div class="page-container">
    <PageHeader>
      <template #left>
        <AccountSetSelector style="width: 140px;" />
        <a-select
          :key="`tpl-select-${templateSelectRevertKey}`"
          v-model:value="selectedTemplateSelectValue"
          :placeholder="currentAccountSetId ? '选择损益模板' : '请先选择账套'"
          :options="templateOptions"
          :loading="templatesLoading"
          :disabled="!currentAccountSetId"
          style="width: 180px"
        />
        <a-button
          class="toolbar-btn"
          :disabled="!selectedTemplateId"
          @click="openEditTemplate"
        >
          <EditOutlined />编辑模板
        </a-button>
        <a-button
          class="toolbar-btn"
          danger
          :disabled="!selectedTemplateId"
          @click="handleDeleteTemplate"
        >
          <DeleteOutlined />删除模板
        </a-button>
        <span class="toolbar-divider" aria-hidden="true"></span>
        <a-tooltip :title="coverageRate === null ? '点击执行覆盖率检查' : `当前覆盖率 ${coverageRate}%`">
          <a-badge
            :status="coverageBadgeColor as any"
            :text="coverageRate === null ? '--' : `${coverageRate}%`"
            :offset="[-4, 0]"
          >
            <a-button
              class="toolbar-btn coverage-btn"
              :disabled="!selectedTemplateId"
              :loading="coverageLoading"
              @click="handleCoverageCheck"
            >
              <SafetyCertificateOutlined />覆盖率检查
            </a-button>
          </a-badge>
        </a-tooltip>
      </template>
      <template #actions>
        <a-button
          class="toolbar-btn"
          type="primary"
          :disabled="!currentAccountSetId"
          @click="handleAddTemplate"
        >
          <PlusOutlined />新增模板
        </a-button>
      </template>
    </PageHeader>

    <!-- Tab 栏：depth=0 group节点 作为 Tab + 右侧全局formula -->
    <div v-if="selectedTemplateId" class="tab-bar">
      <div class="tabs-and-formulas">
        <a-tabs
          v-model:active-key="activeTabKey"
          type="card"
          class="tab-tabs"
          :tab-bar-gutter="4"
        >
          <template #rightExtra>
            <a-tooltip title="新增Tab（depth=0 分组节点）">
              <a-button size="small" type="text" :loading="tabSaveLoading" @click="handleAddTab">
                <template #icon><PlusOutlined /></template>
                Tab
              </a-button>
            </a-tooltip>
          </template>
          <!-- 固定"指标分区"特别 Tab：始终最左，无改名/删除入口 -->
          <a-tab-pane :key="indicatorTabNode.id">
            <template #tab>
              <span
                class="dir-tab-label indicator-tab"
                :class="{ 'indicator-tab--drop-active': indicatorTabDropActive }"
                @dragover="onIndicatorTabDragOver"
                @dragleave="onIndicatorTabDragLeave"
                @drop.prevent="onIndicatorTabDrop"
              >
                <BarChartOutlined />
                <span>{{ indicatorTabNode.name }}</span>
                <span class="dir-tab-count">({{ indicatorTabNode.childCount }})</span>
              </span>
            </template>
          </a-tab-pane>
          <a-tab-pane
            v-for="tab in tabNodes"
            :key="tab.id"
          >
            <template #tab>
              <span class="dir-tab-label">
                <span>{{ tab.name }}</span>
                <span class="dir-tab-count">({{ tab.childCount }})</span>
                <EditOutlined
                  class="dir-tab-edit"
                  title="编辑Tab名称"
                  @click.stop="openTabEdit(tab)"
                />
                <CloseOutlined
                  class="dir-tab-del"
                  title="删除Tab"
                  @click.stop="handleDeleteTab(tab)"
                />
              </span>
            </template>
          </a-tab-pane>
        </a-tabs>
        <!-- 全局formula管理入口（Tab栏右侧） -->
        <div v-if="globalFormulaNodes.length" class="global-formula-area">
          <span
            v-for="gf in globalFormulaNodes"
            :key="gf.id"
            class="global-formula-chip"
            :title="gf.formula || '未设置公式'"
            @click="openGlobalFormulaEdit(gf)"
          >
            <span class="global-formula-chip__name">{{ gf.name }}</span>
            <span v-if="gf.formula" class="global-formula-chip__fx">fx</span>
          </span>
          <a-button size="small" type="dashed" @click="handleAddGlobalFormula">
            <PlusOutlined /> 全局公式
          </a-button>
        </div>
        <div v-else class="global-formula-area">
          <a-button size="small" type="dashed" @click="handleAddGlobalFormula">
            <PlusOutlined /> 全局公式
          </a-button>
        </div>
      </div>
    </div>

    <div class="main-content toolbar-adjacent-card">
      <!-- 左栏：损益项目树（仅渲染当前 Tab 下的项目） -->
      <div class="left-panel" :style="{ width: leftPanelWidth + 'px', flex: 'none' }">
        <div class="tree-toolbar">
          <a-input
            v-model:value="searchText"
            placeholder="搜索项目名称..."
            allow-clear
            size="small"
            style="flex: 1; min-width: 0;"
            @change="handleSearch"
          >
            <template #prefix><SearchOutlined /></template>
          </a-input>
          <a-button size="small" @click="handleAddItem(null)">
            <template #icon><PlusOutlined /></template>
            新增项目
          </a-button>
        </div>
        <a-spin :spinning="itemsLoading">
          <template v-if="!selectedTemplateId">
            <a-empty description="请先选择模板" />
          </template>
          <template v-else>
            <a-tree
              v-if="treeData.length"
              :tree-data="treeData"
              :selected-keys="selectedKeys"
              :expanded-keys="expandedKeys"
              :draggable="draggableConfig as any"
              block-node
              :allow-drop="handleAllowDrop"
              @select="handleTreeSelect"
              @drop="handleTreeDrop"
              @dragstart="onTreeDragStart"
              @dragend="onTreeDragEnd"
              @expand="handleTreeExpand"
            >
              <template #title="nodeData">
                <div class="tree-node-title" :class="{ 'tree-node-group': nodeData.nodeRole === 'group' || nodeData.itemCategory === 'section' }">
                  <span>{{ nodeData.title }}</span>
                  <a-tag v-if="(nodeData.nodeRole === 'data' || nodeData.itemCategory === 'revenue' || nodeData.itemCategory === 'cost') && nodeData.dataSource" size="small" :color="dataSourceColor(nodeData.dataSource)">
                    {{ dataSourceLabel(nodeData.dataSource) }}
                  </a-tag>
                  <a-tag v-if="nodeData.nodeRole === 'formula' || nodeData.itemCategory === 'profit'" size="small" color="purple">公式</a-tag>
                  <a-tag v-if="nodeData.nodeRole === 'indicator' || nodeData.itemCategory === 'indicator'" size="small" color="gold">指标</a-tag>
                </div>
              </template>
            </a-tree>
            <a-empty
              v-else
              description="该Tab下暂无项目，可点击上方「新增项目」按钮添加"
            />
          </template>
        </a-spin>
      </div>

      <!-- 拖拽分割线 -->
      <div class="panel-resizer" @mousedown="onResizerMouseDown">
        <div class="resizer-line"></div>
      </div>

      <!-- 右栏：编辑面板 -->
      <div class="right-panel">
        <!-- 项目编辑面板 -->
        <template v-if="editMode === 'item' && selectedItem">
          <div class="edit-header">
            <span class="edit-title">{{ itemForm.name || '编辑项目' }} <span v-if="isFormDirty" class="dirty-dot" title="未保存修改"></span></span>
            <a-space>
              <a-button size="small" @click="handleCloneCurrentItem">
                <CopyOutlined />复制到其他模板
              </a-button>
              <a-button size="small" danger @click="handleDeleteItem">删除此项</a-button>
            </a-space>
          </div>
          <a-form :model="itemForm" :label-col="{ style: { width: '80px' } }" class="edit-form">
            <!-- ① 基本信息 -->
            <div class="editor-card editor-card--basic">
              <div class="editor-card__head" style="display: flex; justify-content: space-between; align-items: center;">
                <span><span class="editor-card__title">① 基本信息</span><span class="editor-card__hint">这是什么项目</span></span>
                <div style="display: flex; align-items: center; gap: 8px;">
                  <span style="font-size: 13px; color: var(--text-2); font-weight: normal;">排序:</span>
                  <a-input-number v-model:value="itemForm.sortOrder" :min="0" :max="9999" style="width: 80px" size="small" />
                </div>
              </div>
              <div class="form-grid-2col">
                <a-form-item label="项目名称" required>
                  <a-input v-model:value="itemForm.name" placeholder="请输入项目名称" />
                </a-form-item>
                <!-- 旧节点角色选择器已隐藏（保留向后兼容） -->
                <a-form-item v-if="false">
                  <template #label>
                    <span @click="onFieldFocus('nodeRole')" style="cursor: pointer">节点角色 <a-tooltip placement="topLeft"><template #title><div style="max-width: 320px"><div>• 分组(group)：树形分组容器，可嵌套子项</div><div>• 数据(data)：实际取数的业务项目</div><div>• 公式(formula)：通过公式引用其他项计算</div><div>• 指标(indicator)：KPI指标，独立展示</div></div></template><QuestionCircleOutlined class="field-help-icon" /></a-tooltip></span>
                  </template>
                  <a-select v-model:value="itemForm.nodeRole" placeholder="请选择">
                    <a-select-option value="group">分组</a-select-option>
                    <a-select-option value="data">数据</a-select-option>
                    <a-select-option value="formula">公式</a-select-option>
                    <a-select-option value="indicator">指标</a-select-option>
                  </a-select>
                </a-form-item>
                <!-- ①′ 项目类别（替代 nodeRole） -->
                <a-form-item label="项目类别" required>
                  <a-radio-group v-model:value="itemForm.itemCategory" :disabled="isUnderIndicatorSection" @change="(e: any) => onItemCategoryChange(typeof e === 'string' ? e : e?.target?.value)">
                    <a-radio value="section">分组</a-radio>
                    <a-radio value="indicator">指标</a-radio>
                    <a-radio value="revenue">收入</a-radio>
                    <a-radio value="cost">成本</a-radio>
                    <a-radio value="profit">利润公式</a-radio>
                  </a-radio-group>
                </a-form-item>
                <!-- ②′ 值来源（非 section 时显示） -->
                <a-form-item v-if="itemForm.itemCategory !== 'section'" label="值来源" required>
                  <a-radio-group v-model:value="itemForm.valueSource" @change="(e: any) => onValueSourceChange(typeof e === 'string' ? e : e?.target?.value)">
                    <a-radio value="system">系统数据</a-radio>
                    <a-radio value="formula">公式计算</a-radio>
                    <a-radio value="manual">手工填报</a-radio>
                  </a-radio-group>
                </a-form-item>
                <!-- ③′ 系统数据源（值来源=system 时显示） -->
                <a-form-item v-if="itemForm.valueSource === 'system'" label="系统数据源">
                  <a-select v-model:value="itemForm.systemDataSource" placeholder="请选择" allow-clear @change="syncLegacyFields">
                    <a-select-option value="voucher">凭证</a-select-option>
                    <a-select-option value="billing">计费</a-select-option>
                    <a-select-option value="estimate">暂估</a-select-option>
                    <a-select-option value="depreciation">折旧</a-select-option>
                  </a-select>
                </a-form-item>
                <a-form-item v-if="itemForm.nodeRole !== 'group'">
                  <template #label>
                    <span @click="onFieldFocus('unit')" style="cursor: pointer">单位</span>
                  </template>
                  <div style="display: flex; gap: 12px; align-items: center;">
                    <a-auto-complete
                      v-model:value="itemForm.unit"
                      :options="unitOptions"
                      placeholder="元/票/KG/%/人"
                      allow-clear
                      style="width: 160px"
                    />
                    <template v-if="isDecimalUnit(itemForm.unit)">
                      <span style="color: var(--text-2); font-size: 13px;">小数位数:</span>
                      <a-select v-model:value="itemDecimalPlacesSelectValue" placeholder="默认2位" allow-clear style="width: 100px">
                        <a-select-option :value="1">1位</a-select-option>
                        <a-select-option :value="2">2位</a-select-option>
                        <a-select-option :value="3">3位</a-select-option>
                        <a-select-option :value="4">4位</a-select-option>
                      </a-select>
                    </template>
                  </div>
                </a-form-item>
              </div>
            </div>

            <!-- ② 数据来源（data/indicator 显示） -->
            <div v-if="itemForm.nodeRole === 'data' || itemForm.nodeRole === 'indicator'" class="editor-card editor-card--source">
              <div class="editor-card__head">
                <span class="editor-card__title">② 数据来源</span>
                <span class="editor-card__hint">数据从哪里来</span>
              </div>
              <a-form-item v-if="false" help="指定本项数据的获取来源">
                <template #label>
                  <span @click="onFieldFocus('dataSourceType')" style="cursor: pointer">数据来源 <a-tooltip placement="topLeft"><template #title><div style="max-width: 320px"><div>• 出港计费：从出港运单计费结果表聚合</div><div>• 凭证：从会计凭证分录按科目/关键词匹配</div><div>• 资产折旧：从资产卡片自动计算折旧金额</div><div>• 暂估数据：从暂估入库数据取数</div><div>• 分摊：由综合成本池按比例分配</div><div>• 计算公式：纯公式计算项</div></div></template><QuestionCircleOutlined class="field-help-icon" /></a-tooltip></span>
                </template>
                <a-select v-model:value="itemForm.dataSourceType" :options="dataSourceOptions" placeholder="请选择" allow-clear />
              </a-form-item>
              <a-form-item>
                <template #label>
                  <span @click="onFieldFocus('perUnitMode')" style="cursor: pointer">单票&均</span>
                </template>
                <a-select v-model:value="itemForm.perUnitMode" placeholder="请选择" allow-clear>
                  <a-select-option value="auto">自动计算（金额/总票数）</a-select-option>
                  <a-select-option value="manual">手工填报</a-select-option>
                  <a-select-option value="none">不显示</a-select-option>
                </a-select>
              </a-form-item>
              <a-form-item label="来源说明">
                <a-input v-model:value="itemForm.dataSourceRemark" placeholder="如：网点管家-有偿流量流向报表" allow-clear />
              </a-form-item>
              <a-form-item v-if="showManualEntrySwitch" label="手工填报">
                <a-switch v-model:checked="itemForm.isManualEntry" />
                <span style="margin-left: 8px; color: var(--text-3); font-size: 12px;">开启后该项数据在报表页手工录入</span>
              </a-form-item>
            </div>

            <!-- ③ 分类规则（仅 data 节点，且 非billing） -->
            <div v-if="showRuleCard" class="editor-card editor-card--rule">
              <div class="editor-card__head">
                <span class="editor-card__title">③ 分类规则</span>
                <span class="editor-card__hint">凭证如何归入本项</span>
              </div>
              <a-form-item v-if="showAccountFilter" help="凭证分录的科目编码前缀匹配此处配置即归入本项">
                <template #label>
                  <span @click="onFieldFocus('accountCodes')" style="cursor: pointer">关联科目 <a-tooltip placement="topLeft"><template #title><div style="max-width: 320px"><div>报表聚合时，凭证分录按优先级分类归入各损益项：</div><div>1. 数据源天然归属</div><div>2. 科目编码前缀匹配</div><div>3. 摘要关键词匹配</div><div>4. 人工分类</div><div>5. 默认归入其他</div></div></template><QuestionCircleOutlined class="field-help-icon" /></a-tooltip></span>
                </template>
                <AccountCodePicker
                  v-model:value="itemForm.accountCodes"
                  :options="accountOptions"
                  placeholder="搜索科目编码或名称"
                  hint="提示：末尾加 * 触发通配批量选择"
                />
              </a-form-item>
              <!-- 科目级辅助过滤列表 -->
              <a-form-item
                v-if="showAccountFilter && itemForm.accountCodes.length"
                help="为每个关联科目独立配置辅助核算过滤。未配置过滤的科目默认匹配全部数据。"
              >
                <template #label>
                  <span @click="onFieldFocus('auxiliaryFilter')" style="cursor: pointer">辅助过滤</span>
                </template>
                <div class="account-filters">
                  <div
                    v-for="code in itemForm.accountCodes"
                    :key="code"
                    class="account-filters__item"
                  >
                    <div class="account-filters__head" @click="toggleAccountExpand(code)">
                      <span class="account-filters__title">
                        <DownOutlined v-if="isAccountExpanded(code)" />
                        <RightOutlined v-else />
                        <span class="account-filters__code">{{ code }}</span>
                        <span class="account-filters__name">{{ getAccountDisplayName(code) }}</span>
                      </span>
                      <span
                        v-if="(itemForm.accountFilters[code]?.length || 0) > 0"
                        class="account-filters__badge account-filters__badge--active"
                      >
                        {{ itemForm.accountFilters[code].length }} 条过滤
                      </span>
                      <span v-else class="account-filters__badge">无过滤</span>
                    </div>
                    <div v-if="isAccountExpanded(code)" class="account-filters__body">
                      <div
                        v-if="!(itemForm.accountFilters[code]?.length)"
                        class="account-filters__empty-tip"
                      >
                        全部数据（不过滤）
                      </div>
                      <template v-else>
                        <div
                          v-for="(cond, idx) in itemForm.accountFilters[code]"
                          :key="idx"
                          class="aux-filter__row"
                        >
                          <a-select
                            v-model:value="cond.auxType"
                            :options="auxTypeOptions"
                            style="width: 140px; flex-shrink: 0"
                            placeholder="辅助类型"
                            @change="(val: any) => onAuxTypeChange(cond, typeof val === 'string' ? val : String(val ?? ''))"
                          />
                          <a-select
                            v-model:value="cond.codes"
                            mode="multiple"
                            placeholder="选择具体辅助项目"
                            style="flex: 1"
                            :options="getAuxItemOptions(cond.auxType)"
                            :loading="isAuxItemsLoading(cond.auxType)"
                            show-search
                            option-filter-prop="label"
                            allow-clear
                          />
                          <a-button type="text" danger size="small" @click="removeAccountFilter(code, idx)">
                            <DeleteOutlined />
                          </a-button>
                        </div>
                      </template>
                      <a-button
                        type="dashed"
                        size="small"
                        style="width: 100%; margin-top: 4px"
                        @click="addAccountFilter(code)"
                      >
                        <PlusOutlined /> 添加过滤条件
                      </a-button>
                    </div>
                  </div>
                </div>
              </a-form-item>
              <a-form-item help="凭证摘要包含此处关键词即归入本项">
                <template #label>
                  <span @click="onFieldFocus('summaryKeywords')" style="cursor: pointer">摘要关键词</span>
                </template>
                <a-select
                  v-model:value="itemForm.summaryKeywords"
                  mode="tags"
                  placeholder="输入后回车添加"
                  style="width: 100%"
                />
              </a-form-item>
            </div>

            <!-- ③′ 计费过滤（仅 data + billing 数据源） -->
            <div v-if="showBillingFilter" class="editor-card editor-card--billing">
              <div class="editor-card__head">
                <span class="editor-card__title">③ 计费数据过滤</span>
                <span class="editor-card__hint">按网点与业务对象缩小取数范围</span>
              </div>
              <a-form-item help="决定从计费数据中取金额、件量还是重量">
                <template #label>
                  <span @click="onFieldFocus('billingFilter')" style="cursor: pointer">取值方式</span>
                </template>
                <a-select
                  v-model:value="itemForm.billingFilter.aggregation"
                  :options="billingAggregationOptions"
                  style="width: 100%"
                />
              </a-form-item>
              <a-form-item label="数据范围" help="是否仅纳入已计价的运单">
                <a-select
                  v-model:value="itemForm.billingFilter.scope"
                  :options="billingScopeOptions"
                  style="width: 100%"
                />
              </a-form-item>
              <a-form-item label="网点范围" help="留空表示不限制网点">
                <a-select
                  v-model:value="itemForm.billingFilter.outlets"
                  mode="multiple"
                  placeholder="可多选，留空不过滤"
                  :options="networkPointOptions"
                  :loading="networkPointLoading"
                  show-search
                  :filter-option="billingOptionFilter"
                  allow-clear
                  style="width: 100%"
                  :max-tag-count="5"
                />
              </a-form-item>
              <a-form-item label="业务对象" help="合并客户/代理/承包区/业务员/驿站；留空不过滤">
                <a-select
                  v-model:value="itemForm.billingFilter.businessObjects"
                  mode="multiple"
                  placeholder="可多选，留空不过滤"
                  :options="businessObjectOptions"
                  :loading="businessObjectLoading"
                  show-search
                  :filter-option="billingOptionFilter"
                  allow-clear
                  style="width: 100%"
                  :max-tag-count="5"
                />
              </a-form-item>
            </div>

            <!-- ④ 计算公式（group可选、formula必填、indicator可选） -->
            <div v-if="showFormulaCard" class="editor-card editor-card--calc">
              <div class="editor-card__head">
                <span class="editor-card__title">④ 计算</span>
                <span class="editor-card__hint">如何计算本项金额</span>
              </div>
              <a-form-item
                v-if="itemForm.nodeRole === 'group'"
                help="不设置则默认子项求和(SUM_CHILDREN)"
              >
                <template #label>
                  <span @click="onFieldFocus('formula')" style="cursor: pointer">分组公式</span>
                </template>
                <div class="formula-preview">
                  <span v-if="itemForm.formula" class="formula-preview__text">{{ itemForm.formula }}</span>
                  <span v-else class="formula-preview__empty">默认：子项求和</span>
                  <a-button size="small" @click="openFormulaModal('section')">编辑</a-button>
                </div>
              </a-form-item>
              <a-form-item
                v-if="itemForm.nodeRole === 'formula' || itemForm.nodeRole === 'indicator'"
                help="引用其他损益项进行加减运算"
              >
                <template #label>
                  <span @click="onFieldFocus('formula')" style="cursor: pointer">计算公式</span>
                </template>
                <div class="formula-preview">
                  <span v-if="itemForm.formula" class="formula-preview__text">{{ itemForm.formula }}</span>
                  <span v-else class="formula-preview__empty">未设置公式</span>
                  <a-button size="small" @click="openFormulaModal('item')">编辑</a-button>
                </div>
              </a-form-item>
            </div>

            <!-- ⑤ 备注 -->
            <div class="editor-card editor-card--remark">
              <div class="editor-card__head">
                <span class="editor-card__title">⑤ 备注</span>
                <span class="editor-card__hint">业务说明（不影响计算）</span>
              </div>
              <a-form-item label="逻辑说明">
                <a-textarea v-model:value="itemForm.calculationLogic" placeholder="如：基础派费+补贴派费+特价业务派费..." :rows="2" />
              </a-form-item>
            </div>
          </a-form>
        </template>
        <div v-else class="empty-guide">
          <div class="empty-guide__icon"><ProfileOutlined /></div>
          <div class="empty-guide__title">选择项目开始编辑</div>
          <div class="empty-guide__desc">点击左侧树节点编辑损益项目</div>
          <a-button type="primary" size="small" style="margin-top: 12px;" @click="handleAddItem(null)">新增项目</a-button>
        </div>
      </div>

      <!-- 右侧助手栏 -->
      <HelperPanel
        v-if="selectedTemplateId"
        v-model:collapsed="helperCollapsed"
        :width="helperWidth"
        :focused-field-key="focusedFieldKey"
        :references="itemReferences"
        :default-tab="helperDefaultTab"
      />
    </div>

    <!-- 底部 ActionBar：dirty 时浮现 -->
    <transition name="action-bar-fade">
      <div
        v-if="editMode === 'item' && isFormDirty"
        class="action-bar"
        role="status"
        aria-live="polite"
      >
        <span class="action-bar__hint">
          <span class="action-bar__dot"></span>
          当前项目有未保存的修改
        </span>
        <a-space>
          <a-button size="small" @click="selectedItem && fillItemForm(selectedItem)">放弃修改</a-button>
          <a-button
            type="primary"
            size="small"
            :loading="itemSaveLoading"
            @click="handleSaveItem"
          >
            保存 (Ctrl+S)
          </a-button>
        </a-space>
      </div>
    </transition>

    <!-- 跨模板复制弹窗 -->
    <a-modal
      v-model:open="cloneTargetVisible"
      title="复制项目到其他模板"
      width="480px"
      centered
      :body-style="modalScrollBodyStyle"
      :destroy-on-close="true"
      @ok="handleConfirmClone"
      :confirm-loading="cloneSubmitting"
    >
      <a-form layout="vertical">
        <a-form-item label="源项目">
          <a-input :value="selectedItem?.itemName || selectedItem?.name" disabled />
        </a-form-item>
        <a-form-item label="目标模板" required>
          <a-select
            v-model:value="cloneTargetTemplateSelectValue"
            :options="cloneTargetOptions"
            placeholder="选择要复制到哪个模板"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item v-if="cloneNeedsParent" label="目标位置" required>
          <a-select
            v-model:value="cloneTargetParentSelectValue"
            :options="cloneTargetParentOptions"
            :loading="cloneTargetParentLoading"
            placeholder="选择复制到目标模板的哪个分组下"
            style="width: 100%"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 快捷键速查浮层 -->
    <a-modal
      v-model:open="shortcutHelpVisible"
      title="快捷键"
      width="360px"
      centered
      :body-style="modalScrollBodyStyle"
      :footer="null"
    >
      <ul class="shortcut-list">
        <li><kbd>Ctrl</kbd>+<kbd>S</kbd><span>保存当前项</span></li>
        <li><kbd>Ctrl</kbd>+<kbd>K</kbd><span>聚焦搜索框</span></li>
        <li><kbd>Ctrl</kbd>+<kbd>D</kbd><span>复制当前项到其他模板</span></li>
        <li><kbd>?</kbd><span>显示/隐藏本快捷键表</span></li>
      </ul>
    </a-modal>

    <!-- 新增模板弹窗 -->
    <a-modal
      v-model:open="tplDialogVisible"
      :title="tplDialogType === 'add' ? '新增模板' : '编辑模板名称'"
      width="400px"
      centered
      :body-style="modalScrollBodyStyle"
      :destroy-on-close="true"
    >
      <a-form :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="模板名称" required>
          <a-input v-model:value="tplNameInput" placeholder="请输入模板名称" />
        </a-form-item>
        <a-form-item v-if="tplDialogType === 'add'" label="复制来源">
          <a-select
            v-model:value="tplSourceTemplateSelectValue"
            placeholder="留空则创建空白模板"
            allow-clear
            show-search
            option-filter-prop="label"
            :options="templateListForClone"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="所属账套" required>
          <a-select
            v-model:value="tplAccountSetId"
            placeholder="选择账套"
            :options="accountSetOptions"
            :disabled="tplDialogType === 'edit'"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="描述">
          <a-input v-model:value="tplDescription" placeholder="可选" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="tplDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="tplSubmitLoading" @click="handleSubmitTemplate">确定</a-button>
      </template>
    </a-modal>

    <!-- 编辑计算公式弹窗 -->
    <a-modal
      v-model:open="formulaModalVisible"
      title="编辑计算公式"
      :width="900"
      centered
      :body-style="formulaModalBodyStyle"
      @ok="onFormulaModalOk"
      @cancel="onFormulaModalCancel"
    >
      <FormulaEditor v-model="tempFormula" :items="currentFormulaItems" />
    </a-modal>

    <!-- Tab 编辑弹窗（编辑depth=0 group节点名称/排序） -->
    <a-modal
      v-model:open="tabEditModalVisible"
      :title="tabEditMode === 'add' ? '新增Tab' : '编辑Tab'"
      width="400px"
      centered
      :body-style="modalScrollBodyStyle"
      :destroy-on-close="true"
      @ok="confirmTabEdit"
    >
      <a-form :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="Tab名称" required>
          <a-input v-model:value="tabEditForm.name" placeholder="如：出港、进港、综合" @press-enter="confirmTabEdit" />
        </a-form-item>
        <a-form-item label="排序">
          <a-input-number v-model:value="tabEditForm.sort" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 全局Formula编辑弹窗 -->
    <a-modal
      v-model:open="globalFormulaEditVisible"
      :title="globalFormulaEditMode === 'add' ? '新增全局公式' : '编辑全局公式'"
      width="640px"
      centered
      :body-style="modalScrollBodyStyle"
      :destroy-on-close="true"
      @ok="confirmGlobalFormulaEdit"
      :confirm-loading="globalFormulaSaving"
    >
      <a-form :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="名称" required>
          <a-input v-model:value="globalFormulaForm.name" placeholder="如：经营净利润" />
        </a-form-item>
        <a-form-item label="排序">
          <a-input-number v-model:value="globalFormulaForm.sort" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item label="公式">
          <FormulaEditor v-model="globalFormulaForm.formula" :items="formulaAvailableItems" />
        </a-form-item>
        <a-button
          v-if="globalFormulaEditMode === 'edit'"
          danger
          block
          @click="handleDeleteGlobalFormula"
        >
          删除该全局公式
        </a-button>
      </a-form>
    </a-modal>

    <!-- 新增损益项弹窗 -->
    <a-modal
      v-model:open="addItemModalVisible"
      :title="addItemForm.isIndicatorSection ? '新增指标分区' : '新增损益项'"
      width="640px"
      centered
      :body-style="modalScrollBodyStyle"
      :destroy-on-close="true"
    >
      <a-form :model="addItemForm" :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }" style="padding: 10px 20px">
        <a-form-item label="项目名称" required>
          <a-input v-model:value="addItemForm.itemName" placeholder="请输入项目名称" />
        </a-form-item>
        <!-- 旧节点角色已隐藏（保留向后兼容） -->
        <a-form-item v-if="false" label="节点角色" required>
          <a-select v-model:value="addItemForm.nodeRole" placeholder="请选择">
            <a-select-option value="group">分组</a-select-option>
            <a-select-option value="data">数据</a-select-option>
            <a-select-option value="formula">公式</a-select-option>
            <a-select-option value="indicator">指标</a-select-option>
          </a-select>
        </a-form-item>
        <!-- 项目类别（替代 nodeRole） -->
        <a-form-item label="项目类别" required>
          <a-radio-group v-model:value="addItemForm.itemCategory" :disabled="checkAncestorIsIndicatorSection(addItemForm.parentId) || addItemForm.isIndicatorSection" @change="(e: any) => onAddItemCategoryChange(typeof e === 'string' ? e : e?.target?.value)">
            <a-radio value="section">分组</a-radio>
            <a-radio value="indicator">指标</a-radio>
            <a-radio value="revenue">收入</a-radio>
            <a-radio value="cost">成本</a-radio>
            <a-radio value="profit">利润公式</a-radio>
          </a-radio-group>
        </a-form-item>
        <!-- 值来源（非 section 时显示） -->
        <a-form-item v-if="addItemForm.itemCategory !== 'section'" label="值来源" required>
          <a-radio-group v-model:value="addItemForm.valueSource" @change="(e: any) => onAddValueSourceChange(typeof e === 'string' ? e : e?.target?.value)">
            <a-radio value="system">系统数据</a-radio>
            <a-radio value="formula">公式计算</a-radio>
            <a-radio value="manual">手工填报</a-radio>
          </a-radio-group>
        </a-form-item>
        <!-- 系统数据源（值来源=system 时显示） -->
        <a-form-item v-if="addItemForm.valueSource === 'system'" label="系统数据源">
          <a-select v-model:value="addItemSystemDataSourceSelectValue" placeholder="请选择" allow-clear @change="syncAddItemLegacyFields">
            <a-select-option value="voucher">凭证</a-select-option>
            <a-select-option value="billing">计费</a-select-option>
            <a-select-option value="estimate">暂估</a-select-option>
            <a-select-option value="depreciation">折旧</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="所属位置" required>
          <a-select
            v-model:value="addItemForm.parentId"
            placeholder="请选择所属位置"
            :options="parentOptions"
            show-search
            :filter-option="(input: string, option: any) => (option?.label ?? '').toLowerCase().includes(input.toLowerCase())"
          />
        </a-form-item>
        <a-form-item v-if="false" label="数据来源">
          <a-select v-model:value="addItemDataSourceSelectValue" :options="dataSourceOptions" placeholder="请选择" allow-clear />
        </a-form-item>
        <a-form-item label="排序">
          <a-input-number v-model:value="addItemForm.sort" :min="0" style="width: 120px" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="addItemModalVisible = false">取消</a-button>
        <a-button type="primary" :loading="addItemSubmitLoading" @click="handleAddItemSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 科目覆盖率诊断抽屉 -->
    <a-drawer
      v-model:open="coverageDrawerVisible"
      title="科目覆盖率诊断"
      :width="640"
      placement="right"
    >
      <a-spin :spinning="coverageLoading">
        <template v-if="coverageReport">
          <div style="margin-bottom: 16px;">
            <a-row :gutter="16">
              <a-col :span="6"><a-statistic title="数据点总数" :value="coverageReport.totalDataPoints" /></a-col>
              <a-col :span="6"><a-statistic title="已匹配" :value="coverageReport.matchedDataPoints" :value-style="{ color: 'var(--color-success)' }" /></a-col>
              <a-col :span="6"><a-statistic title="未匹配" :value="coverageReport.unmatchedDataPoints" :value-style="{ color: 'var(--color-danger)' }" /></a-col>
              <a-col :span="6"><a-statistic title="覆盖率" :value="coverageReport.coverageRate" suffix="%" /></a-col>
            </a-row>
          </div>
          <div v-if="coverageReport.uncoveredAccounts?.length">
            <div style="font-weight: 600; margin-bottom: 8px;">未覆盖科目明细</div>
            <a-table
              :dataSource="coverageReport.uncoveredAccounts"
              :columns="coverageColumns"
              :pagination="false"
              size="small"
              :row-key="(r: any) => r.accountCode"
              bordered
            />
          </div>
        </template>
        <a-empty v-else description="请点击「覆盖率检查」按钮开始诊断" />
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted, nextTick } from 'vue'
import { message, Modal } from 'ant-design-vue'
import {
  PlusOutlined,
  DeleteOutlined,
  QuestionCircleOutlined,
  EditOutlined,
  SearchOutlined,
  SafetyCertificateOutlined,
  CloseOutlined,
  ProfileOutlined,
  CopyOutlined,
  DownOutlined,
  RightOutlined,
  BarChartOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import FormulaEditor from '@/components/FormulaEditor.vue'
import HelperPanel from './AmoebaPLTemplate/HelperPanel.vue'
import AccountCodePicker from './AmoebaPLTemplate/AccountCodePicker.vue'
import { useUnsavedGuard } from './AmoebaPLTemplate/useUnsavedGuard'
import { useTemplateShortcuts } from './AmoebaPLTemplate/useTemplateShortcuts'
import {
  getAmoebaPLTemplates,
  getAmoebaPLTemplateById,
  createAmoebaPLTemplate,
  cloneAmoebaPLTemplate,
  updateAmoebaPLTemplate,
  deleteAmoebaPLTemplate,
  addAmoebaPLItem,
  updateAmoebaPLItem,
  deleteAmoebaPLItem,
  reorderAmoebaPLItems,
  getAccountTree,
  getAmoebaCoverageReport,
  cloneItemFromTemplate,
  getAuxiliaryTypes,
  getAuxiliaryItemsByAccountSet,
} from '@/api/finance'
import {
  getExpNetworkPointOptions,
  getExpAgentList,
  getExpFranchiseAreaOptions,
  getExpLastMileStationOptions,
  getSalesmenList,
} from '@/api/express'
import { getCustomerList } from '@/api/crm'
import { useAccountSetStore } from '@/stores/accountSet'
import { useOrgContextStore } from '@/stores/orgContext'
import { auxTypeLabel } from '@/constants/auxTypes'

const accountSetStore = useAccountSetStore()
const orgContextStore = useOrgContextStore()

// ==================== 右侧助手栏状态 ====================
const helperCollapsed = ref<boolean>(true)
const helperWidth = ref<number>(360)
const focusedFieldKey = ref<string | undefined>(undefined)
const helperDefaultTab = ref<string>('field')

function onFieldFocus(fieldKey: string) {
  focusedFieldKey.value = fieldKey
  helperDefaultTab.value = 'field'
  // 折叠状态下点字段标签没有任何可见反馈，自动展开助手栏
  helperCollapsed.value = false
}

// ==================== 左右面板拖拽调宽 ====================
const leftPanelWidth = ref(300)
let isResizing = false
let startX = 0
let startWidth = 0

function onResizerMouseDown(e: MouseEvent) {
  isResizing = true
  startX = e.clientX
  startWidth = leftPanelWidth.value
  document.addEventListener('mousemove', onResizerMouseMove)
  document.addEventListener('mouseup', onResizerMouseUp)
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'
}

function onResizerMouseMove(e: MouseEvent) {
  if (!isResizing) return
  const delta = e.clientX - startX
  const newWidth = startWidth + delta
  leftPanelWidth.value = Math.max(240, Math.min(600, newWidth))
}

function onResizerMouseUp() {
  isResizing = false
  document.removeEventListener('mousemove', onResizerMouseMove)
  document.removeEventListener('mouseup', onResizerMouseUp)
  document.body.style.cursor = ''
  document.body.style.userSelect = ''
}

// ==================== 数据源选项 ====================
const dataSourceOptions = [
  { value: 'billing', label: '出港计费' },
  { value: 'voucher', label: '凭证' },
  { value: 'depreciation', label: '资产折旧' },
  { value: 'estimate', label: '暂估数据' },
  { value: 'formula', label: '计算公式' },
  { value: 'allocation', label: '分摊' },
]

// ==================== 模板列表 ====================
const templates = ref<any[]>([])
const templatesLoading = ref(false)
const selectedTemplateId = ref<number | null>(null)
let loadTemplatesGeneration = 0

function toNumberOrNull(value: unknown): number | null {
  if (value === null || value === undefined || value === '') return null
  const numberValue = Number(value)
  return Number.isFinite(numberValue) ? numberValue : null
}

function toStringOrNull(value: unknown): string | null {
  if (value === null || value === undefined || value === '') return null
  return String(value)
}

const selectedTemplateSelectValue = computed<any>({
  get: () => selectedTemplateId.value ?? undefined,
  set: value => handleTemplateChange(toNumberOrNull(value)),
})

const templateOptions = computed(() =>
  templates.value.map(t => {
    // 兜底：name 可能为空字符串，避免 a-select 在 label 为空/undefined 时回退显示 value(ID)
    const rawName = (t.name ?? t.fName ?? '').toString().trim()
    return { value: t.id, label: rawName || `未命名模板 #${t.id}` }
  })
)

const accountSetOptions = computed(() =>
  accountSetStore.accountSets.map(a => ({ value: a.id, label: a.fName }))
)

const currentAccountSetId = computed<number | null>(() => accountSetStore.getCurrentAccountSetId() ?? null)

async function loadTemplates() {
  const gen = ++loadTemplatesGeneration
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) {
    templates.value = []
    selectedTemplateId.value = null
    return
  }
  templatesLoading.value = true
  try {
    const res = await getAmoebaPLTemplates({ accountSetId })
    if (gen !== loadTemplatesGeneration) return // 丢弃过期响应
    templates.value = Array.isArray(res) ? res : (res as any)?.items || []
    // 验证当前选中模板是否存在于新加载的列表中
    const validIds = templates.value.map((t: any) => t.id)
    if (selectedTemplateId.value && !validIds.includes(selectedTemplateId.value)) {
      selectedTemplateId.value = null
      // 作废在途的损益项请求并清空残留树
      loadItemsGeneration++
      flatItems.value = []
      itemsLoading.value = false
    }
    if (templates.value.length > 0 && !selectedTemplateId.value) {
      selectedTemplateId.value = templates.value[0].id
      await loadTemplateItems()
    }
  } catch {
    if (gen !== loadTemplatesGeneration) return
    templates.value = []
  } finally {
    if (gen === loadTemplatesGeneration) {
      templatesLoading.value = false
    }
  }
}

// 取消切换时重挂载下拉，强制其显示值回弹到受控 prop（antd-vue 受控组件的内部态不会自动回退）
const templateSelectRevertKey = ref(0)

function handleTemplateChange(val: number | null) {
  if (val === selectedTemplateId.value) return
  const apply = () => {
    selectedTemplateId.value = val
    selectedItem.value = null
    editMode.value = null
    // 上一模板的展开状态/搜索词/覆盖率徽标对新模板无意义，一并重置
    expandedKeys.value = []
    searchText.value = ''
    coverageReport.value = null
    if (val) loadTemplateItems()
  }
  // 切换模板会清空编辑面板，同样要走未保存守卫
  if (isFormDirty.value) {
    Modal.confirm({
      title: '未保存的修改',
      content: '切换模板将丢弃当前项目未保存的修改，是否继续？',
      okText: '丢弃并切换',
      okType: 'danger',
      cancelText: '取消',
      onOk: apply,
      onCancel: () => { templateSelectRevertKey.value++ },
    })
    return
  }
  apply()
}

watch(() => accountSetStore.currentAccountSetId, () => {
  // 账套是全局上下文，此处无法拦截切换；若有未保存修改至少要明确告知，不能静默丢弃
  if (isFormDirty.value) {
    message.warning('账套已切换，编辑中项目的未保存修改已丢弃')
  }
  selectedTemplateId.value = null
  selectedItem.value = null
  editMode.value = null
  // 作废在途的损益项请求，防止旧模板响应迟到后把已清空的树写回
  loadItemsGeneration++
  flatItems.value = []
  itemsLoading.value = false
  loadTemplates()
  loadAccountOptions()
})

// ==================== 新增/编辑模板 ====================
const tplDialogVisible = ref(false)
const tplDialogType = ref<'add' | 'edit'>('add')
const tplNameInput = ref('')
const tplAccountSetId = ref<number | undefined>(undefined)
const tplDescription = ref('')
const tplSourceTemplateId = ref<number | null>(null)
const tplSubmitLoading = ref(false)
const editingTemplateId = ref<number | null>(null)

const tplSourceTemplateSelectValue = computed<any>({
  get: () => tplSourceTemplateId.value ?? undefined,
  set: value => { tplSourceTemplateId.value = toNumberOrNull(value) },
})

// 复制来源下拉选项：基于已加载的模板列表，编辑模式排除当前模板
const templateListForClone = computed(() => {
  return templates.value
    .filter((t: any) => editingTemplateId.value == null || t.id !== editingTemplateId.value)
    .map((t: any) => {
      const rawName = (t.name ?? t.fName ?? '').toString().trim() || `未命名模板 #${t.id}`
      const accSetId = t.accountSetId
      return { value: t.id, label: accSetId ? `${rawName} (账套${accSetId})` : rawName }
    })
})

function handleAddTemplate() {
  tplDialogType.value = 'add'
  tplNameInput.value = ''
  tplAccountSetId.value = accountSetStore.getCurrentAccountSetId() || undefined
  tplDescription.value = ''
  tplSourceTemplateId.value = null
  editingTemplateId.value = null
  tplDialogVisible.value = true
}

function openEditTemplate() {
  if (!selectedTemplateId.value) return
  const tpl = templates.value.find(t => t.id === selectedTemplateId.value)
  if (!tpl) return
  tplDialogType.value = 'edit'
  tplNameInput.value = tpl.name || tpl.fName || ''
  tplAccountSetId.value = tpl.accountSetId || accountSetStore.getCurrentAccountSetId() || undefined
  tplDescription.value = tpl.description || tpl.fDescription || ''
  tplSourceTemplateId.value = null
  editingTemplateId.value = selectedTemplateId.value
  tplDialogVisible.value = true
}

function handleDeleteTemplate() {
  if (!selectedTemplateId.value) return
  const tpl = templates.value.find(t => t.id === selectedTemplateId.value)
  const tplName = (tpl?.name || tpl?.fName || '').toString().trim() || `#${selectedTemplateId.value}`
  Modal.confirm({
    title: '删除模板',
    content: `确定删除模板「${tplName}」吗？其下全部损益项将一并删除，且不可恢复。`,
    okText: '删除',
    okType: 'danger',
    cancelText: '取消',
    async onOk() {
      try {
        await deleteAmoebaPLTemplate(selectedTemplateId.value!)
        message.success('模板已删除')
        selectedTemplateId.value = null
        selectedItem.value = null
        editMode.value = null
        loadItemsGeneration++
        flatItems.value = []
        expandedKeys.value = []
        searchText.value = ''
        coverageReport.value = null
        await loadTemplates()
      } catch (e: any) {
        message.error(e?.message || '删除失败')
      }
    },
  })
}

async function handleSubmitTemplate() {
  if (!tplNameInput.value.trim()) { message.warning('请输入模板名称'); return }
  if (!tplAccountSetId.value) { message.warning('请选择所属账套'); return }
  tplSubmitLoading.value = true
  try {
    if (tplDialogType.value === 'add') {
      if (tplSourceTemplateId.value) {
        // 基于已有模板复制创建
        const res: any = await cloneAmoebaPLTemplate(tplSourceTemplateId.value, {
          name: tplNameInput.value.trim(),
          accountSetId: tplAccountSetId.value,
          description: tplDescription.value || undefined,
        })
        tplDialogVisible.value = false
        await loadTemplates()
        const newId = res?.data?.id ?? res?.id ?? null
        // 统一走切换流程：重置编辑态并加载新模板的损益项，避免下拉显示新模板而左树仍是旧模板。
        // 复制到其他账套时新模板不属于当前列表，不自动切换
        if (newId && tplAccountSetId.value === accountSetStore.getCurrentAccountSetId()) {
          handleTemplateChange(newId)
        }
        message.success('模板复制成功')
      } else {
        await createAmoebaPLTemplate({
          name: tplNameInput.value.trim(),
          accountSetId: tplAccountSetId.value,
          description: tplDescription.value || null,
        })
        tplDialogVisible.value = false
        await loadTemplates()
        message.success('创建成功')
      }
    } else {
      await updateAmoebaPLTemplate(editingTemplateId.value!, {
        name: tplNameInput.value.trim(),
        description: tplDescription.value || null,
      })
      tplDialogVisible.value = false
      await loadTemplates()
      message.success('更新成功')
    }
  } catch (e: any) {
    message.error(e?.message || '操作失败')
  } finally {
    tplSubmitLoading.value = false
  }
}

// ==================== 损益项数据（统一树模型） ====================
const flatItems = ref<any[]>([])
const itemsLoading = ref(false)
const selectedItem = ref<any>(null)
const selectedKeys = ref<string[]>([])
const expandedKeys = ref<string[]>([])
const editMode = ref<'item' | null>(null)

// 将后端嵌套树扁平化为列表
function flattenNestedItems(items: any[], parentId: number = 0): any[] {
  const result: any[] = []
  for (const node of items) {
    result.push({ ...node, parentId, children: undefined })
    if (node.children?.length) {
      result.push(...flattenNestedItems(node.children, node.id))
    }
  }
  return result
}

// 竞态守卫：快速切换模板/账套时响应乱序返回，旧模板的损益项不得覆盖当前树
let loadItemsGeneration = 0

async function loadTemplateItems() {
  if (!selectedTemplateId.value) return
  const gen = ++loadItemsGeneration
  itemsLoading.value = true
  try {
    const res = await getAmoebaPLTemplateById(selectedTemplateId.value)
    if (gen !== loadItemsGeneration) return // 丢弃过期响应
    const nestedItems = res?.items || res?.children || []
    flatItems.value = flattenNestedItems(nestedItems)
    nextTick(() => {
      // 默认展开所有 group 节点
      const groupKeys = flatItems.value
        .filter(i => getNodeRole(i) === 'group')
        .map(i => `item-${i.id}`)
      if (expandedKeys.value.length === 0) {
        expandedKeys.value = groupKeys
      }
    })
    // 自动选中第一个Tab；无普通Tab时默认激活固定指标Tab——否则其哨兵态下 activeTabId 恒为 null，
    // 在该Tab点「新增项目」时 isIndicatorTabActive 为假、无法触发懒创建
    if (!activeTabId.value) {
      const firstTab = tabNodes.value[0]
      activeTabId.value = firstTab ? firstTab.id : indicatorTabNode.value.id
    }
  } catch {
    if (gen !== loadItemsGeneration) return
    flatItems.value = []
  } finally {
    if (gen === loadItemsGeneration) {
      itemsLoading.value = false
    }
  }
}

// 获取节点角色（兼容新旧字段名）
function getNodeRole(item: any): string {
  return item.nodeRole || item.fNodeRole || ''
}

function getItemName(item: any): string {
  return item.itemName || item.name || item.fName || ''
}

function getItemSort(item: any): number {
  return item.sort ?? item.sortOrder ?? item.fSortOrder ?? item.fSort ?? 0
}

// ==================== Tab 逻辑（depth=0 group 节点） ====================
const activeTabId = ref<number | null>(null)
const tabSaveLoading = ref(false)

const activeTabKey = computed<any>({
  get: () => activeTabId.value ?? undefined,
  set: value => {
    const next = toNumberOrNull(value)
    if (next === activeTabId.value) return
    // 用户切换 Tab：关闭右栏编辑面板并清空选中，避免残留上一个 Tab 的选中项。
    // 脏数据按账套切换的既有口径——提示已丢弃（不阻断切换）。
    if (isFormDirty.value) message.warning('已切换Tab，未保存的修改已丢弃')
    activeTabId.value = next
    selectedKeys.value = []
    selectedItem.value = null
    editMode.value = null
  },
})

// Tab节点 = depth=0 group节点
const tabNodes = computed(() => {
  return flatItems.value
    .filter(i => (!i.parentId || i.parentId === 0) && getNodeRole(i) === 'group' && !i.isIndicatorSection)
    .sort((a, b) => getItemSort(a) - getItemSort(b))
    .map(i => ({
      id: i.id,
      name: getItemName(i),
      childCount: flatItems.value.filter(c => c.parentId === i.id).length,
      sort: getItemSort(i),
      item: i,
    }))
})

// ==================== 固定"指标分区"特别 Tab ====================
// 指标分区按设计为全局唯一、根级；编辑器把它固定成标签栏最左的特别 Tab，
// 不再钉在每个普通 Tab 顶部。分区尚未创建时用哨兵 id，首次在该 Tab 新增指标项时懒创建。
const INDICATOR_TAB_ID = -1

const indicatorSectionItem = computed(() =>
  flatItems.value.find(i => i.isIndicatorSection && (!i.parentId || i.parentId === 0)) || null
)

const indicatorTabNode = computed(() => {
  const sec = indicatorSectionItem.value
  return {
    id: sec ? sec.id : INDICATOR_TAB_ID,
    name: sec ? getItemName(sec) : '运营指标',
    childCount: sec ? flatItems.value.filter(c => c.parentId === sec.id).length : 0,
  }
})

const isIndicatorTabActive = computed(() => activeTabId.value === indicatorTabNode.value.id)

// 全局formula节点 = depth=0 formula节点
const globalFormulaNodes = computed(() => {
  return flatItems.value
    .filter(i => (!i.parentId || i.parentId === 0) && getNodeRole(i) === 'formula')
    .sort((a, b) => getItemSort(a) - getItemSort(b))
    .map(i => ({
      id: i.id,
      name: getItemName(i),
      formula: i.formula || i.fFormula || '',
      sort: getItemSort(i),
      item: i,
    }))
})

// Tab列表变更后，如果当前activeTab不在列表中，自动选中第一个
watch(tabNodes, (tabs) => {
  // 指标分区固定Tab始终合法，不参与回退（其 id 不在 tabNodes 中）
  if (isIndicatorTabActive.value) return
  if (tabs.length === 0) {
    activeTabId.value = null
    return
  }
  if (!tabs.find(t => t.id === activeTabId.value)) {
    activeTabId.value = tabs[0].id
  }
})

// ==================== 树形数据（当前Tab下的子树） ====================
function buildItemNode(item: any): any {
  const children = flatItems.value
    .filter(i => i.parentId === item.id)
    .sort((a, b) => getItemSort(a) - getItemSort(b))
  const role = getNodeRole(item)
  return {
    key: `item-${item.id}`,
    title: getItemName(item),
    nodeRole: role,
    itemData: item,
    dataSource: item.dataSource || item.dataSourceType || item.fDataSourceType || '',
    itemCategory: item.itemCategory || '',
    isIndicatorSection: item.isIndicatorSection || false,
    children: children.length > 0 ? children.map(c => buildItemNode(c)) : undefined,
  }
}

// ==================== 搜索 ====================
const searchText = ref('')

// 递归过滤树节点：保留命中节点及其祖先
function filterTreeNode(node: any, keyword: string): any | null {
  const titleMatch = String(node.title || '').toLowerCase().includes(keyword)
  const filteredChildren = (node.children || [])
    .map((c: any) => filterTreeNode(c, keyword))
    .filter((c: any) => c !== null)
  if (titleMatch || filteredChildren.length > 0) {
    return {
      ...node,
      children: filteredChildren.length > 0 ? filteredChildren : undefined,
    }
  }
  return null
}

const treeData = computed(() => {
  const nodes: any[] = []

  // 当前Tab（含固定指标分区Tab）下的项目；指标分区不再全局置顶到每个 Tab
  if (activeTabId.value) {
    const roots = flatItems.value
      .filter(i => i.parentId === activeTabId.value)
      .sort((a, b) => getItemSort(a) - getItemSort(b))
    nodes.push(...roots.map(r => buildItemNode(r)))
  }

  const keyword = searchText.value.trim().toLowerCase()
  if (!keyword) return nodes
  return nodes
    .map(n => filterTreeNode(n, keyword))
    .filter((n: any) => n !== null)
})

function handleSearch() {
  const keyword = searchText.value.trim().toLowerCase()
  if (!keyword) return
  nextTick(() => {
    // 查找匹配项（仅当前 Tab 子树）
    const inCurrentTab = (item: any): boolean => {
      let pid = item.parentId
      const visited = new Set<number>()
      while (pid && pid !== 0 && !visited.has(pid)) {
        if (pid === activeTabId.value) return true
        visited.add(pid)
        const parent = flatItems.value.find(i => i.id === pid)
        pid = parent?.parentId || 0
      }
      return false
    }
    const inIndicatorSection = (item: any): boolean => {
      if (isIndicatorSectionNode(item)) return true
      let pid = item.parentId
      const visited = new Set<number>()
      while (pid && pid !== 0 && !visited.has(pid)) {
        visited.add(pid)
        const parent = flatItems.value.find(i => i.id === pid)
        if (parent && isIndicatorSectionNode(parent)) return true
        pid = parent?.parentId || 0
      }
      return false
    }
    const matched = flatItems.value.filter(i =>
      (inCurrentTab(i) || inIndicatorSection(i)) && getItemName(i).toLowerCase().includes(keyword)
    )
    if (matched.length === 0) return
    // 展开包含匹配项的所有祖先节点
    const keysToExpand = new Set(expandedKeys.value)
    for (const item of matched) {
      let pid = item.parentId
      while (pid && pid !== 0 && pid !== activeTabId.value) {
        keysToExpand.add(`item-${pid}`)
        const parent = flatItems.value.find(i => i.id === pid)
        pid = parent?.parentId || 0
      }
    }
    expandedKeys.value = [...keysToExpand]
  })
}

// ==================== 树事件处理 ====================
/** 取消选中（清空编辑面板），带未保存守卫；供点击已选节点与 Esc 快捷键共用 */
function requestDeselect() {
  const applyDeselect = () => {
    selectedKeys.value = []
    editMode.value = null
    selectedItem.value = null
  }
  if (isFormDirty.value) {
    Modal.confirm({
      title: '未保存的修改',
      content: '当前项目有未保存的修改，是否放弃？',
      okText: '放弃',
      okType: 'danger',
      cancelText: '取消',
      onOk: applyDeselect,
    })
    return
  }
  applyDeselect()
}

function handleTreeSelect(keys: any[], { node }: any) {
  if (keys.length === 0) {
    // 点击已选中节点（antd 默认取消选中）同样要走未保存守卫，不能静默丢弃修改
    requestDeselect()
    return
  }
  if (node.itemData) {
    // 高亮在确认放弃修改后才更新，避免"高亮指向 B、面板仍在编辑 A"的错位
    selectItem(node.itemData, () => {
      selectedKeys.value = keys.map(key => String(key))
    })
  } else {
    selectedKeys.value = keys.map(key => String(key))
  }
}

function handleTreeExpand(keys: any[]) {
  expandedKeys.value = keys.map(key => String(key))
}

const draggableConfig: any = {
  // 所有节点（含 depth>=1 的子节点）均可拖拽
  nodeDraggable: () => true,
}

function handleAllowDrop(info: any) {
  // 仅分组节点可作为容器接收"拖入内部"（dropPosition===0）；
  // data/formula/indicator 是叶子，拖入其内部会产生汇总逻辑不认的层级
  const { dropNode, dropPosition } = info || {}
  if (dropPosition === 0) {
    const target = flatItems.value.find(i => i.id === parseItemKey(dropNode?.key))
    if (target && getNodeRole(target) !== 'group') return false
  }
  return true
}

/**
 * 拖拽落点结构校验（与后端 ReorderItemsAsync 的校验保持同口径）。
 * 返回错误文案；合法返回 null。
 */
function validateDropTarget(dragItem: any, newParentId: number): string | null {
  // 根级只允许 Tab/全局公式/指标分区存在；普通项落到根级后任何 UI 都不渲染（幽灵项）
  if (!newParentId) {
    return '不能移动到根级位置（仅板块、全局公式与指标分区可在根级）'
  }
  const parentItem = flatItems.value.find(i => i.id === newParentId)
  if (!parentItem) {
    return '目标父级不存在，请刷新后重试'
  }
  if (getNodeRole(parentItem) !== 'group') {
    return '只能放入分组节点内'
  }
  // 指标分区（含其子分组）下只能放 indicator 类别的项——仅认根级标记，
  // 嵌套分组上的历史残留误标不构成指标分区
  const intoIndicatorSection = isIndicatorSectionNode(parentItem) || checkAncestorIsIndicatorSection(newParentId)
  const dragIsIndicator = (dragItem.itemCategory || '') === 'indicator' || getNodeRole(dragItem) === 'indicator'
  if (intoIndicatorSection && !dragIsIndicator) {
    return '指标分区下只能放置指标类项目'
  }
  if (!intoIndicatorSection && isIndicatorSectionNode(dragItem)) {
    return '指标分区不能移入业务板块内'
  }
  return null
}

// 解析树节点 key（形如 "item-123"）→ 数据库 id
function parseItemKey(key: any): number {
  const k = String(key ?? '')
  const id = parseInt(k.replace('item-', ''))
  return Number.isNaN(id) ? 0 : id
}

// ==================== 拖指标项到「指标Tab」改归属（单向） ====================
const draggingItemId = ref<number | null>(null)
const indicatorTabDropActive = ref(false)

function isIndicatorLeaf(item: any): boolean {
  return !!item && ((item.itemCategory || '') === 'indicator' || getNodeRole(item) === 'indicator')
}

function onTreeDragStart(info: any) {
  draggingItemId.value = parseItemKey(info?.node?.key)
}
function onTreeDragEnd() {
  draggingItemId.value = null
  indicatorTabDropActive.value = false
}

function onIndicatorTabDragOver(e: DragEvent) {
  const item = flatItems.value.find(i => i.id === draggingItemId.value)
  if (item && isIndicatorLeaf(item)) {
    e.preventDefault() // 允许放置（仅指标项）
    indicatorTabDropActive.value = true
  }
}
function onIndicatorTabDragLeave(e: DragEvent) {
  // 子元素穿越时浏览器会先触发外层 dragleave，再触发子元素 dragenter；
  // 仅在光标真正离开整个 span（含子元素）时才清除高亮，避免闪烁
  if (e.currentTarget && (e.currentTarget as Element).contains(e.relatedTarget as Node)) return
  indicatorTabDropActive.value = false
}

async function onIndicatorTabDrop() {
  indicatorTabDropActive.value = false
  const itemId = draggingItemId.value
  draggingItemId.value = null
  if (!itemId || !selectedTemplateId.value) return
  let item = flatItems.value.find(i => i.id === itemId)
  if (!item) return
  if (!isIndicatorLeaf(item)) { message.warning('只有指标项可移入指标分区'); return }
  try {
    const secId = await ensureIndicatorSection()
    if (!secId) return // ensureIndicatorSection 失败时已给提示
    item = flatItems.value.find(i => i.id === itemId) || item // ensure 可能已重载
    if ((item.parentId ?? 0) === secId) { message.info('该指标已在指标分区中'); return }
    await updateAmoebaPLItem(selectedTemplateId.value, itemId, buildItemUpdatePayload(item, {
      parentId: secId,
      sort: computeNextSortOrder(secId),
    }))
    const savedKeys = [...expandedKeys.value]
    await loadTemplateItems()
    expandedKeys.value = [...new Set([...savedKeys, `item-${secId}`])]
    // 被移走的若正是当前选中项，移入指标分区后它已不在当前 Tab，清空右栏编辑面板
    if (selectedItem.value?.id === itemId) {
      selectedItem.value = null
      selectedKeys.value = []
      editMode.value = null
    }
    message.success('已移入指标分区')
  } catch (e: any) {
    message.error(e?.message || '移入指标分区失败')
  }
}

async function handleTreeDrop(info: any) {
  const { dragNode, node: dropNode, dropToGap } = info
  const dragId = parseItemKey(dragNode?.key)
  const dropId = parseItemKey(dropNode?.key)
  if (!dragId || !dropId) {
    message.warning('无法识别拖拽节点')
    return
  }

  // 始终基于原始 flatItems 计算父子关系/兄弟列表，
  // 避免依赖 treeData（搜索过滤后的副本）或 dropNode.itemData（antd-vue 可能未透传自定义字段）。
  const dragItemRaw = flatItems.value.find(i => i.id === dragId)
  const dropItemRaw = flatItems.value.find(i => i.id === dropId)
  if (!dragItemRaw || !dropItemRaw) {
    message.warning('节点数据已变更，请刷新后重试')
    return
  }

  const savedExpandedKeys = [...expandedKeys.value]

  // 计算目标父级（从原始数据派生，不依赖可能被搜索过滤的 treeData）
  let newParentId: number
  let insertAtHead = !dropToGap
  if (!dropToGap) {
    // 拖入 dropNode 内部 → dropNode 成为新父节点
    newParentId = dropId
  } else {
    // 作为 dropNode 的兄弟插入 → 使用 dropNode 的真实 parentId
    newParentId = dropItemRaw.parentId ?? (activeTabId.value || 0)
    // 指标分区恒置顶展示，其旁的根级 gap 实际用户意图是"插到当前板块子项首位"
    if (!newParentId && isIndicatorSectionNode(dropItemRaw) && activeTabId.value) {
      newParentId = activeTabId.value
      insertAtHead = true
    }
  }

  // 防御：禁止把节点拖到自身或其子孙下，避免循环
  const isDescendant = (ancestorId: number, childId: number, visited = new Set<number>()): boolean => {
    if (!childId || childId === 0) return false
    if (childId === ancestorId) return true
    if (visited.has(childId)) return false // 历史脏数据成环防护
    visited.add(childId)
    const child = flatItems.value.find(i => i.id === childId)
    if (!child) return false
    return isDescendant(ancestorId, child.parentId || 0, visited)
  }
  if (newParentId === dragId || isDescendant(dragId, newParentId)) {
    message.warning('不能将节点拖入其自身或其子节点')
    return
  }

  // 结构合法性校验：禁止落到根级、落入叶子、非指标项进指标分区等
  const dropError = validateDropTarget(dragItemRaw, newParentId)
  if (dropError) {
    message.warning(dropError)
    return
  }

  // 当前父节点下的兄弟（已剔除被拖拽节点本身），按现有排序排列
  const siblingsRaw = flatItems.value
    .filter(i => (i.parentId ?? 0) === newParentId && i.id !== dragId)
    .sort((a, b) => getItemSort(a) - getItemSort(b))

  // 计算插入位置
  // 说明：antd-vue Tree 在拖到分组内首个子项的上半部分时，会把 dropTargetKey 切换为前一个
  // 展平节点（即分组自身）并触发 dropPosition=0 / dropToGap=false。因此 !dropToGap 实际
  // 包含两种用户意图：(1) 拖到首个子项上方 -> 期望排在最前；(2) 拖到分组（含空分组）行上。
  // 统一按“插入到子节点列表头部”处理，避免出现“拖到第一个位置却落到末尾”的反直觉行为。
  let insertIdx: number
  if (insertAtHead) {
    insertIdx = 0
  } else {
    const dropIdxInSiblings = siblingsRaw.findIndex(s => s.id === dropId)
    if (dropIdxInSiblings === -1) {
      message.warning('无法确定插入位置')
      return
    }
    // info.dropPosition = 相对位置(-1|0|1) + dropNode 在其父节点 children 中的下标
    // 还原相对位置：-1=上方、1=下方（dropToGap=true 时不会出现 0）
    const dropNodeSiblingIdx = Number(String(dropNode.pos || '0').split('-').pop())
    const relativePos = info.dropPosition - dropNodeSiblingIdx
    insertIdx = relativePos < 0 ? dropIdxInSiblings : dropIdxInSiblings + 1
  }

  // 构造新顺序，并按 (idx+1)*10 重新分配排序值
  const newOrder: { id: number }[] = [...siblingsRaw]
  newOrder.splice(insertIdx, 0, { id: dragId })

  const payload = newOrder.map((s, idx) => ({
    itemId: s.id,
    sort: (idx + 1) * 10,
    parentId: newParentId,
  }))

  try {
    await reorderAmoebaPLItems(selectedTemplateId.value!, payload)
    message.success('排序已更新')
    await loadTemplateItems()
    const keysToRestore = new Set(savedExpandedKeys)
    if (newParentId) keysToRestore.add(`item-${newParentId}`)
    expandedKeys.value = [...keysToRestore]
  } catch (e: any) {
    message.error(e?.message || '排序失败')
  }
}

// ==================== Tab管理（depth=0 group CRUD） ====================
const tabEditModalVisible = ref(false)
const tabEditMode = ref<'add' | 'edit'>('add')
const tabEditForm = reactive({ name: '', sort: 0 })
const tabEditingItem = ref<any>(null)

function handleAddTab() {
  tabEditMode.value = 'add'
  tabEditForm.name = ''
  tabEditForm.sort = (tabNodes.value.length + 1) * 10
  tabEditingItem.value = null
  tabEditModalVisible.value = true
}

function openTabEdit(tab: { id: number; name: string; sort: number; item: any }) {
  tabEditMode.value = 'edit'
  tabEditForm.name = tab.name
  tabEditForm.sort = tab.sort
  tabEditingItem.value = tab.item
  tabEditModalVisible.value = true
}

function normalizeJsonField(value: any) {
  if (value === undefined || value === null || value === '') return null
  return typeof value === 'string' ? value : JSON.stringify(value)
}

function buildItemUpdatePayload(item: any, overrides: Record<string, any> = {}) {
  return {
    itemName: getItemName(item),
    nodeRole: getNodeRole(item),
    dataSource: item.dataSource ?? item.dataSourceType ?? item.fDataSourceType ?? null,
    itemCategory: item.itemCategory ?? item.f项目类别 ?? null,
    valueSource: item.valueSource ?? item.f值来源 ?? null,
    systemDataSource: item.systemDataSource ?? item.f系统数据源 ?? null,
    isIndicatorSection: item.isIndicatorSection ?? item.f是否指标分区 ?? false,
    summaryKeywordsJson: normalizeJsonField(item.summaryKeywordsJson ?? item.summaryKeywords ?? item.fSummaryKeywords),
    relatedAccountsJson: normalizeJsonField(item.relatedAccountsJson ?? item.accountCodes ?? item.fAccountCodes),
    formula: item.formula ?? item.fFormula ?? null,
    sort: getItemSort(item),
    unit: item.unit ?? item.fUnit ?? null,
    perUnitMode: item.perUnitMode ?? item.fPerUnitMode ?? null,
    decimalPlaces: item.decimalPlaces ?? item.f小数位数 ?? null,
    dataSourceRemark: item.dataSourceRemark ?? item.fDataSourceRemark ?? null,
    calculationLogic: item.calculationLogic ?? item.fCalculationLogic ?? null,
    isManualEntry: item.isManualEntry ?? item.fIsManualEntry ?? false,
    billingFilterJson: normalizeJsonField(item.billingFilterJson ?? item.fBillingFilterJson),
    ...overrides,
  }
}

async function confirmTabEdit() {
  const name = tabEditForm.name.trim()
  if (!name) { message.warning('请输入Tab名称'); return }
  if (!selectedTemplateId.value) return
  tabSaveLoading.value = true
  try {
    if (tabEditMode.value === 'add') {
      // 新增一个 depth=0 group 节点（排序输入框可被清空为 null，兜底为 0 防后端 400）
      await addAmoebaPLItem(selectedTemplateId.value, {
        itemName: name,
        nodeRole: 'group',
        parentId: 0,
        sort: Number(tabEditForm.sort) || 0,
      })
      message.success('Tab已创建')
    } else {
      // 编辑已有Tab节点的名称/排序
      await updateAmoebaPLItem(selectedTemplateId.value, tabEditingItem.value.id, buildItemUpdatePayload(tabEditingItem.value, {
        itemName: name,
        sort: Number(tabEditForm.sort) || 0,
      }))
      message.success('Tab已更新')
    }
    tabEditModalVisible.value = false
    await loadTemplateItems()
  } catch (e: any) {
    message.error(e?.message || '操作失败')
  } finally {
    tabSaveLoading.value = false
  }
}

/** 统计节点的全部后代数量（带防环守卫） */
function countDescendants(rootId: number): number {
  let count = 0
  const stack = [rootId]
  const visited = new Set<number>([rootId])
  while (stack.length) {
    const cur = stack.pop()!
    for (const child of flatItems.value.filter(i => i.parentId === cur)) {
      if (visited.has(child.id)) continue
      visited.add(child.id)
      count++
      stack.push(child.id)
    }
  }
  return count
}

/** 判断 itemId 是否等于 rootId 或位于其子树内（带防环守卫） */
function isWithinSubtree(itemId: number | undefined, rootId: number): boolean {
  if (!itemId) return false
  let cur: number | undefined = itemId
  const visited = new Set<number>()
  while (cur && cur !== 0 && !visited.has(cur)) {
    if (cur === rootId) return true
    visited.add(cur)
    cur = flatItems.value.find(i => i.id === cur)?.parentId
  }
  return false
}

function handleDeleteTab(tab: { id: number; name: string; childCount: number }) {
  // 级联删除是整棵子树：按全部后代计数，直接子级数会低估删除范围
  const descendantCount = countDescendants(tab.id)
  const content = descendantCount > 0
    ? `Tab「${tab.name}」下共有 ${descendantCount} 个子项目（含嵌套），删除后这些项目也将被删除。确认？`
    : `确认删除Tab「${tab.name}」？`
  Modal.confirm({
    title: '删除Tab',
    content,
    okType: 'danger',
    async onOk() {
      try {
        await deleteAmoebaPLItem(selectedTemplateId.value!, tab.id)
        message.success('Tab已删除')
        if (activeTabId.value === tab.id) activeTabId.value = null
        // 当前编辑项位于被删 Tab 内时清理编辑面板，避免面板残留已删除项目
        if (isWithinSubtree(selectedItem.value?.id, tab.id)) {
          selectedItem.value = null
          editMode.value = null
          selectedKeys.value = []
        }
        await loadTemplateItems()
      } catch (e: any) {
        message.error(e?.message || '删除失败')
      }
    },
  })
}

// ==================== 全局Formula管理 ====================
const globalFormulaEditVisible = ref(false)
const globalFormulaEditMode = ref<'add' | 'edit'>('add')
const globalFormulaForm = reactive({ name: '', sort: 0, formula: '' })
const globalFormulaEditingItem = ref<any>(null)
const globalFormulaSaving = ref(false)

function handleDeleteGlobalFormula() {
  const item = globalFormulaEditingItem.value
  if (!item || !selectedTemplateId.value) return
  Modal.confirm({
    title: '删除全局公式',
    content: `确定删除全局公式「${getItemName(item)}」吗？`,
    okText: '删除',
    okType: 'danger',
    cancelText: '取消',
    async onOk() {
      try {
        await deleteAmoebaPLItem(selectedTemplateId.value!, item.id)
        message.success('已删除')
        globalFormulaEditVisible.value = false
        await loadTemplateItems()
      } catch (e: any) {
        // 被其他公式引用时后端会拦截并说明引用方
        message.error(e?.message || '删除失败')
      }
    },
  })
}

function handleAddGlobalFormula() {
  globalFormulaEditMode.value = 'add'
  globalFormulaForm.name = ''
  globalFormulaForm.sort = (globalFormulaNodes.value.length + 1) * 10
  globalFormulaForm.formula = ''
  globalFormulaEditingItem.value = null
  globalFormulaEditVisible.value = true
}

function openGlobalFormulaEdit(gf: { id: number; name: string; formula: string; sort: number; item: any }) {
  globalFormulaEditMode.value = 'edit'
  globalFormulaForm.name = gf.name
  globalFormulaForm.sort = gf.sort
  globalFormulaForm.formula = gf.formula
  globalFormulaEditingItem.value = gf.item
  globalFormulaEditVisible.value = true
}

async function confirmGlobalFormulaEdit() {
  const name = globalFormulaForm.name.trim()
  if (!name) { message.warning('请输入名称'); return }
  if (!selectedTemplateId.value) return
  // 全局公式同样校验引用与自引用
  const allowed = new Set(formulaAvailableItems.value.map(i => i.name))
  const formulaError = validateFormulaText(globalFormulaForm.formula || '', allowed, name)
  if (formulaError) {
    message.warning(formulaError)
    return
  }
  globalFormulaSaving.value = true
  try {
    if (globalFormulaEditMode.value === 'add') {
      await addAmoebaPLItem(selectedTemplateId.value, {
        itemName: name,
        nodeRole: 'formula',
        parentId: 0,
        sort: Number(globalFormulaForm.sort) || 0,
        formula: globalFormulaForm.formula || null,
      })
      message.success('全局公式已创建')
    } else {
      await updateAmoebaPLItem(selectedTemplateId.value, globalFormulaEditingItem.value.id, buildItemUpdatePayload(globalFormulaEditingItem.value, {
        itemName: name,
        sort: Number(globalFormulaForm.sort) || 0,
        formula: globalFormulaForm.formula || null,
      }))
      message.success('全局公式已更新')
    }
    globalFormulaEditVisible.value = false
    await loadTemplateItems()
  } catch (e: any) {
    message.error(e?.message || '操作失败')
  } finally {
    globalFormulaSaving.value = false
  }
}

// ==================== 选中项 & 编辑表单 ====================
const itemForm = reactive({
  name: '',
  nodeRole: '' as string,
  dataSourceType: '' as string,
  itemCategory: '' as string,
  valueSource: '' as string,
  systemDataSource: '' as string,
  isIndicatorSection: false as boolean,
  summaryKeywords: [] as string[],
  accountCodes: [] as string[],
  // 科目级独立辅助过滤：{ 科目编码: [{auxType, codes}] }
  accountFilters: {} as Record<string, { auxType: string; codes: string[] }[]>,
  formula: '',
  sortOrder: 0,
  unit: '' as string,
  perUnitMode: '' as string,
  decimalPlaces: null as number | null,
  dataSourceRemark: '' as string,
  calculationLogic: '' as string,
  isManualEntry: false as boolean,
  billingFilter: {
    outlets: [] as string[],
    businessObjects: [] as string[],
    aggregation: 'amount' as 'amount' | 'waybill_count' | 'weight',
    scope: 'priced' as 'priced' | 'all',
  },
})

const itemDecimalPlacesSelectValue = computed<any>({
  get: () => itemForm.decimalPlaces ?? undefined,
  set: value => { itemForm.decimalPlaces = toNumberOrNull(value) },
})
const itemSaveLoading = ref(false)
const itemFormSnapshot = ref('')

function getFormSnapshot() {
  return JSON.stringify({
    name: itemForm.name,
    nodeRole: itemForm.nodeRole,
    dataSourceType: itemForm.dataSourceType,
    itemCategory: itemForm.itemCategory,
    valueSource: itemForm.valueSource,
    systemDataSource: itemForm.systemDataSource,
    isIndicatorSection: itemForm.isIndicatorSection,
    summaryKeywords: itemForm.summaryKeywords,
    accountCodes: itemForm.accountCodes,
    accountFilters: itemForm.accountFilters,
    formula: itemForm.formula,
    sortOrder: itemForm.sortOrder,
    unit: itemForm.unit,
    perUnitMode: itemForm.perUnitMode,
    decimalPlaces: itemForm.decimalPlaces,
    dataSourceRemark: itemForm.dataSourceRemark,
    calculationLogic: itemForm.calculationLogic,
    isManualEntry: itemForm.isManualEntry,
    billingFilter: itemForm.billingFilter,
  })
}

const isFormDirty = computed(() => {
  if (!selectedItem.value) return false
  return getFormSnapshot() !== itemFormSnapshot.value
})

// ==================== 二维正交选择器联动逻辑 ====================

/** 是否已有指标分区（仅认根级标记，与报表服务和树渲染同口径） */
const hasIndicatorSection = computed(() => !!indicatorSectionItem.value)

/**
 * 是否为有效的指标分区节点：仅认根级标记。
 * V4 迁移前的历史库中，Tab 内嵌套分组（出港指标/进港指标）可能残留误标，
 * 按普通分组对待，否则拖拽排序/类别编辑会被指标分区规则误拦
 */
function isIndicatorSectionNode(item: any): boolean {
  return !!item?.isIndicatorSection && (!item.parentId || item.parentId === 0)
}

/** 检查祖先是否为指标分区（仅认根级标记） */
function checkAncestorIsIndicatorSection(parentId: number | null | undefined, visited = new Set<number>()): boolean {
  if (!parentId || parentId === 0) return false
  if (visited.has(parentId)) return false // 历史脏数据成环防护
  visited.add(parentId)
  const parent = flatItems.value.find(i => i.id === parentId)
  if (!parent) return false
  if (isIndicatorSectionNode(parent)) return true
  return checkAncestorIsIndicatorSection(parent.parentId, visited)
}

/** 当前编辑项是否在指标分区下 */
const isUnderIndicatorSection = computed(() => {
  return checkAncestorIsIndicatorSection(selectedItem.value?.parentId)
})

/** 向后兼容：同步旧字段 */
function syncLegacyFields() {
  if (itemForm.itemCategory === 'section') itemForm.nodeRole = 'group'
  else if (itemForm.itemCategory === 'indicator') itemForm.nodeRole = 'indicator'
  else if (itemForm.itemCategory === 'profit') itemForm.nodeRole = 'formula'
  else itemForm.nodeRole = 'data'

  if (itemForm.valueSource === 'system') itemForm.dataSourceType = itemForm.systemDataSource || ''
  else if (itemForm.valueSource === 'formula') itemForm.dataSourceType = 'formula'
  else if (itemForm.valueSource === 'manual') itemForm.dataSourceType = 'manual'
  else itemForm.dataSourceType = ''
}

/** 项目类别变更联动 */
function onItemCategoryChange(val: string) {
  if (val === 'section') {
    itemForm.valueSource = ''
    itemForm.systemDataSource = ''
  }
  if (val === 'profit') {
    itemForm.valueSource = 'formula'
  }
  syncLegacyFields()
}

/** 值来源变更联动 */
function onValueSourceChange(val: string) {
  if (val !== 'system') {
    itemForm.systemDataSource = ''
  }
  syncLegacyFields()
}

/** 新增弹窗：向后兼容同步旧字段 */
function syncAddItemLegacyFields() {
  if (addItemForm.itemCategory === 'section') addItemForm.nodeRole = 'group'
  else if (addItemForm.itemCategory === 'indicator') addItemForm.nodeRole = 'indicator'
  else if (addItemForm.itemCategory === 'profit') addItemForm.nodeRole = 'formula'
  else addItemForm.nodeRole = 'data'

  if (addItemForm.valueSource === 'system') addItemForm.dataSource = addItemForm.systemDataSource
  else if (addItemForm.valueSource === 'formula') addItemForm.dataSource = 'formula'
  else if (addItemForm.valueSource === 'manual') addItemForm.dataSource = 'manual'
  else addItemForm.dataSource = null
}

/** 新增弹窗：项目类别变更联动 */
function onAddItemCategoryChange(val: string) {
  if (val === 'section') {
    addItemForm.valueSource = ''
    addItemForm.systemDataSource = null
  }
  if (val === 'profit') {
    addItemForm.valueSource = 'formula'
  }
  syncAddItemLegacyFields()
}

/** 新增弹窗：值来源变更联动 */
function onAddValueSourceChange(val: string) {
  if (val !== 'system') {
    addItemForm.systemDataSource = null
  }
  syncAddItemLegacyFields()
}

const unitOptions = [
  { value: '元' }, { value: '票' }, { value: 'KG' }, { value: '%' },
  { value: '人' }, { value: '件/人/日' }, { value: 'KM' },
  { value: '米/方' }, { value: '平米' }, { value: '票/元' }
]

// 根据单位判断是否显示小数
function isDecimalUnit(unit: string | null | undefined): boolean {
  if (!unit) return true // 无单位默认按“元”处理
  const u = unit.toLowerCase()
  if (u === '元' || u === '%') return true
  if (u.includes('率')) return true
  if (u === 'kg') return true
  if (u === 'km') return true
  return false
}

// 手工填报开关显示条件
const showManualEntrySwitch = computed(() => {
  const r = itemForm.nodeRole
  if (r === 'group' || r === 'formula') return false
  if (r === 'indicator') return true
  // data 节点：自动数据源时隐藏
  const autoSources = ['voucher', 'billing', 'depreciation', 'estimate', 'allocation', 'formula']
  if (itemForm.valueSource === 'system') return false
  return true
})

// 辅助过滤数据源限制：仅 voucher / depreciation / estimate 显示关联科目 + 科目级过滤
const accountFilterSources = ['voucher', 'depreciation', 'estimate']
const showAccountFilter = computed(
  () => itemForm.nodeRole === 'data' && itemForm.valueSource === 'system' && accountFilterSources.includes(itemForm.systemDataSource || ''),
)

// 计费过滤区域显示条件（仅 data 节点 + 计费数据源）
const showBillingFilter = computed(
  () => itemForm.valueSource === 'system' && itemForm.systemDataSource === 'billing'
)

// “关联科目 + 摘要关键词”分类规则卡片显示条件（billing 时隐藏）
const showRuleCard = computed(() => itemForm.nodeRole === 'data' && !(itemForm.valueSource === 'system' && itemForm.systemDataSource === 'billing'))

// 公式卡片显示条件
const showFormulaCard = computed(() => {
  const r = itemForm.nodeRole
  return r === 'group' || r === 'formula' || r === 'indicator'
})

// 辅助类型选项（从后端 API 动态加载，编码→中文映射收敛到 @/constants/auxTypes）
const auxTypeOptions = ref<{ value: string; label: string }[]>([])
async function loadAuxTypes() {
  try {
    const types = await getAuxiliaryTypes()
    auxTypeOptions.value = (types || []).map((t: any) => {
      const name = t?.name || t?.code || ''
      return { value: name, label: auxTypeLabel(name) }
    }).filter(o => !!o.value)
  } catch (e) {
    console.error('加载辅助类型列表失败', e)
    auxTypeOptions.value = []
  }
}

// 辅助项目按类型缓存：{ auxType: [{value: code, label: 'code-name'}] }
const auxItemsCache = ref<Record<string, { value: string; label: string }[]>>({})
const auxItemsLoading = ref<Record<string, boolean>>({})

async function loadAuxItems(type: string) {
  if (!type) return
  // 已加载或加载中则跳过
  if (auxItemsCache.value[type]) return
  if (auxItemsLoading.value[type]) return
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) {
    auxItemsCache.value[type] = []
    return
  }
  auxItemsLoading.value = { ...auxItemsLoading.value, [type]: true }
  try {
    const list = await getAuxiliaryItemsByAccountSet({ accountSetId, auxType: type })
    const items = (list || []).map((it: any) => ({
      value: it.code || it.fCode || '',
      label: `${it.code || it.fCode || ''}-${it.name || it.fName || ''}`,
    })).filter((o: { value: string }) => !!o.value)
    auxItemsCache.value = { ...auxItemsCache.value, [type]: items }
  } catch (e) {
    console.error(`加载辅助项目列表失败(type=${type})`, e)
    auxItemsCache.value = { ...auxItemsCache.value, [type]: [] }
  } finally {
    auxItemsLoading.value = { ...auxItemsLoading.value, [type]: false }
  }
}

function getAuxItemOptions(type: string) {
  if (!type) return []
  return auxItemsCache.value[type] || []
}

function isAuxItemsLoading(type: string) {
  return !!(type && auxItemsLoading.value[type])
}

function onAuxTypeChange(cond: { auxType: string; codes: string[] }, newType: string) {
  // 切换辅助类型时清空已选编码（避免跨类型残留）
  cond.codes = []
  if (newType) loadAuxItems(newType)
}

// 切换账套时清空辅助项目缓存
watch(() => accountSetStore.currentAccountSetId, () => {
  auxItemsCache.value = {}
  auxItemsLoading.value = {}
})

// ==================== 科目级辅助过滤交互 ====================
const expandedAccountCodes = ref<Set<string>>(new Set())
function toggleAccountExpand(code: string) {
  const s = new Set(expandedAccountCodes.value)
  if (s.has(code)) s.delete(code)
  else s.add(code)
  expandedAccountCodes.value = s
}
function isAccountExpanded(code: string) {
  return expandedAccountCodes.value.has(code)
}
function addAccountFilter(code: string) {
  if (!itemForm.accountFilters[code]) itemForm.accountFilters[code] = []
  const defaultType = auxTypeOptions.value[0]?.value || 'outlet'
  itemForm.accountFilters[code].push({ auxType: defaultType, codes: [] })
  // 预加载默认类型的辅助项目列表
  if (defaultType) loadAuxItems(defaultType)
  // 添加后自动展开
  if (!expandedAccountCodes.value.has(code)) {
    const s = new Set(expandedAccountCodes.value)
    s.add(code)
    expandedAccountCodes.value = s
  }
}
function removeAccountFilter(code: string, idx: number) {
  const list = itemForm.accountFilters[code]
  if (!list) return
  list.splice(idx, 1)
}
function getAccountDisplayName(code: string) {
  const opt = accountOptions.value.find(o => o.value === code)
  if (!opt) return ''
  // label 格式为 "code-一级-二级-..."，取最后一段名称
  const parts = (opt.label || '').split('-')
  return parts.length > 1 ? parts[parts.length - 1] : ''
}

// ==================== 计费过滤：网点 / 业务对象选项 ====================
type BillingAggregation = 'amount' | 'waybill_count' | 'weight'
type BillingScope = 'priced' | 'all'

const billingAggregationOptions: { value: BillingAggregation; label: string }[] = [
  { value: 'amount', label: '金额' },
  { value: 'waybill_count', label: '件量' },
  { value: 'weight', label: '重量' },
]

const billingScopeOptions: { value: BillingScope; label: string }[] = [
  { value: 'priced', label: '仅已计价' },
  { value: 'all', label: '全部运单' },
]

function parseBillingFilter(json: string | null | undefined): { outlets: string[]; businessObjects: string[]; aggregation: BillingAggregation; scope: BillingScope } {
  if (!json) return { outlets: [], businessObjects: [], aggregation: 'amount', scope: 'priced' }
  try {
    const obj = typeof json === 'string' ? JSON.parse(json) : json
    const aggRaw = (obj as any)?.aggregation
    const aggregation: BillingAggregation = (aggRaw === 'waybill_count' || aggRaw === 'weight') ? aggRaw : 'amount'
    const scopeRaw = (obj as any)?.scope
    const scope: BillingScope = scopeRaw === 'all' ? 'all' : 'priced'
    return {
      outlets: Array.isArray((obj as any)?.outlets) ? (obj as any).outlets : [],
      businessObjects: Array.isArray((obj as any)?.businessObjects) ? (obj as any).businessObjects : [],
      aggregation,
      scope,
    }
  } catch { return { outlets: [], businessObjects: [], aggregation: 'amount', scope: 'priced' } }
}

const networkPointOptions = ref<{ label: string; value: string }[]>([])
const networkPointLoading = ref(false)
const businessObjectOptions = ref<{ label: string; value: string }[]>([])
const businessObjectLoading = ref(false)

// 网点/业务对象是组织隔离数据：切换组织后清空缓存，
// 否则用户会从旧组织的网点/客户列表里选 code 存进新组织的模板（懒加载机制会按需重新拉取）
watch(() => orgContextStore.orgSwitchVersion, () => {
  networkPointOptions.value = []
  businessObjectOptions.value = []
  if (itemForm.dataSourceType === 'billing') {
    loadNetworkPointOptions()
    loadBusinessObjectOptions()
  }
})

async function loadNetworkPointOptions() {
  if (networkPointLoading.value) return
  networkPointLoading.value = true
  try {
    const res: any = await getExpNetworkPointOptions({ pageIndex: 1, pageSize: 200 })
    const items: any[] = res?.items ?? []
    networkPointOptions.value = items
      .filter((p: any) => p && p.code)
      .map((p: any) => ({
        label: `${p.shortName || p.code} (${p.code})`,
        value: p.code as string,
      }))
  } catch {
    networkPointOptions.value = []
  } finally {
    networkPointLoading.value = false
  }
}

async function loadBusinessObjectOptions() {
  if (businessObjectLoading.value) return
  businessObjectLoading.value = true
  try {
    const [crmRes, agentRes, faRes, salesRes, lmsRes] = await Promise.all([
      getCustomerList({ pageIndex: 1, pageSize: 200 }).catch(() => null),
      getExpAgentList({ pageIndex: 1, pageSize: 200 }).catch(() => null),
      getExpFranchiseAreaOptions({ pageIndex: 1, pageSize: 200 }).catch(() => null),
      getSalesmenList({ pageIndex: 1, pageSize: 200 }).catch(() => null),
      getExpLastMileStationOptions({ pageIndex: 1, pageSize: 200 }).catch(() => null),
    ])
    const opts: { label: string; value: string }[] = []
    const crmItems: any[] = (crmRes as any)?.items ?? []
    crmItems.forEach((c: any) => {
      const code = c.code || c.customerCode
      if (code) opts.push({ label: `[客户] ${c.shortName || code} (${code})`, value: String(code) })
    })
    const agentItems: any[] = (agentRes as any)?.items ?? []
    agentItems.forEach((a: any) => {
      if (a?.code) opts.push({ label: `[代理] ${a.name || a.code} (${a.code})`, value: String(a.code) })
    })
    const faItems: any[] = (faRes as any)?.items ?? []
    faItems.forEach((f: any) => {
      if (f?.code) opts.push({ label: `[承包区] ${f.contractor || f.coverageDistrict || f.code} (${f.code})`, value: String(f.code) })
    })
    const salesItems: any[] = (salesRes as any)?.items ?? []
    salesItems.forEach((s: any) => {
      if (s?.employeeNo) opts.push({ label: `[业务员] ${s.name || s.employeeNo} (${s.employeeNo})`, value: String(s.employeeNo) })
    })
    const lmsItems: any[] = (lmsRes as any)?.items ?? []
    lmsItems.forEach((l: any) => {
      if (l?.code) opts.push({ label: `[驿站] ${l.name || l.code} (${l.code})`, value: String(l.code) })
    })
    businessObjectOptions.value = opts
  } catch {
    businessObjectOptions.value = []
  } finally {
    businessObjectLoading.value = false
  }
}

function billingOptionFilter(input: string, option: any) {
  const label = String(option?.label ?? '').toLowerCase()
  const value = String(option?.value ?? '').toLowerCase()
  const kw = input.toLowerCase()
  return label.includes(kw) || value.includes(kw)
}

watch(() => itemForm.dataSourceType, (val) => {
  if (val === 'billing') {
    if (!networkPointOptions.value.length && !networkPointLoading.value) loadNetworkPointOptions()
    if (!businessObjectOptions.value.length && !businessObjectLoading.value) loadBusinessObjectOptions()
  }
})

function selectItem(item: any, onApply?: () => void) {
  const apply = () => {
    onApply?.()
    doSelectItem(item)
  }
  if (isFormDirty.value) {
    Modal.confirm({
      title: '未保存的修改',
      content: '当前项目有未保存的修改，是否放弃？',
      okText: '放弃',
      okType: 'danger',
      cancelText: '取消',
      onOk: apply,
    })
    return
  }
  apply()
}

function doSelectItem(item: any) {
  selectedItem.value = item
  editMode.value = 'item'
  fillItemForm(item)
}

/** legacy data 项的类别推导：沿父链找名称含"收入"的祖先 → revenue，否则 cost（与迁移脚本同口径） */
function deriveLegacyCategory(item: any): string {
  let pid = item.parentId
  const visited = new Set<number>()
  while (pid && pid !== 0 && !visited.has(pid)) {
    visited.add(pid)
    const parent = flatItems.value.find(i => i.id === pid)
    if (!parent) break
    if (getItemName(parent).includes('收入')) return 'revenue'
    pid = parent.parentId
  }
  return 'cost'
}

function fillItemForm(item: any) {
  itemForm.name = getItemName(item)
  itemForm.nodeRole = getNodeRole(item)
  itemForm.dataSourceType = item.dataSource || item.dataSourceType || item.fDataSourceType || ''

  // 新字段优先，否则从旧字段推导（向后兼容）
  if (item.itemCategory) {
    itemForm.itemCategory = item.itemCategory
    itemForm.valueSource = item.valueSource || ''
    itemForm.systemDataSource = item.systemDataSource || ''
    itemForm.isIndicatorSection = item.isIndicatorSection || false
    syncLegacyFields()
  } else {
    const role = getNodeRole(item)
    if (role === 'group') itemForm.itemCategory = 'section'
    else if (role === 'indicator') itemForm.itemCategory = 'indicator'
    else if (role === 'formula') itemForm.itemCategory = 'profit'
    // legacy data 项按所属 Tab 名称推导（与 FinanceSeeder.MigrateV3 同口径）：
    // 一律默认 revenue 会把成本项随下一次保存静默写成收入类别
    else itemForm.itemCategory = deriveLegacyCategory(item)

    const ds = itemForm.dataSourceType
    if (['voucher', 'billing', 'estimate', 'depreciation'].includes(ds)) {
      itemForm.valueSource = 'system'
      itemForm.systemDataSource = ds
    } else if (ds === 'formula') {
      itemForm.valueSource = 'formula'
    } else {
      itemForm.valueSource = 'manual'
    }
    itemForm.isIndicatorSection = item.isIndicatorSection || false
  }
  try {
    const kw = item.summaryKeywordsJson || item.summaryKeywords || item.fSummaryKeywords
    itemForm.summaryKeywords = kw ? (typeof kw === 'string' ? JSON.parse(kw) : kw) : []
  } catch { itemForm.summaryKeywords = [] }
  try {
    const ac = item.relatedAccountsJson || item.accountCodes || item.fAccountCodes
    if (ac) {
      const parsed = typeof ac === 'string' ? JSON.parse(ac) : ac
      const result = parseRelatedAccounts(parsed)
      itemForm.accountCodes = result.codes
      itemForm.accountFilters = result.filters
      // 编辑已有项目时，预加载所有用到的辅助类型对应的项目列表
      const usedTypes = new Set<string>()
      Object.values(result.filters || {}).forEach((arr: any) => {
        ;(arr || []).forEach((c: any) => { if (c?.auxType) usedTypes.add(c.auxType) })
      })
      usedTypes.forEach(t => loadAuxItems(t))
    } else {
      itemForm.accountCodes = []
      itemForm.accountFilters = {}
    }
  } catch {
    itemForm.accountCodes = []
    itemForm.accountFilters = {}
  }
  itemForm.formula = item.formula || item.fFormula || ''
  itemForm.sortOrder = getItemSort(item)
  itemForm.unit = item.unit || item.fUnit || ''
  itemForm.perUnitMode = item.perUnitMode || item.fPerUnitMode || ''
  itemForm.decimalPlaces = item.decimalPlaces ?? item.f小数位数 ?? null
  itemForm.dataSourceRemark = item.dataSourceRemark || item.fDataSourceRemark || ''
  itemForm.calculationLogic = item.calculationLogic || item.fCalculationLogic || ''
  itemForm.isManualEntry = item.isManualEntry ?? item.fIsManualEntry ?? false
  itemForm.billingFilter = parseBillingFilter(item.billingFilterJson ?? item.fBillingFilterJson ?? null)
  nextTick(() => { itemFormSnapshot.value = getFormSnapshot() })
}

// 解析关联科目 JSON（仅支持新对象数组格式：[{ code, filters: [{auxType, codes}] }]）
function parseRelatedAccounts(parsed: any): {
  codes: string[]
  filters: Record<string, { auxType: string; codes: string[] }[]>
} {
  if (!Array.isArray(parsed) || parsed.length === 0) {
    return { codes: [], filters: {} }
  }
  const codes: string[] = []
  const filters: Record<string, { auxType: string; codes: string[] }[]> = {}
  for (const item of parsed) {
    if (item && typeof item.code === 'string') {
      codes.push(item.code)
      if (Array.isArray(item.filters) && item.filters.length) {
        filters[item.code] = item.filters
          .filter((f: any) => f && typeof f.auxType === 'string')
          .map((f: any) => ({
            auxType: f.auxType,
            codes: Array.isArray(f.codes) ? f.codes.map((c: any) => String(c)) : [],
          }))
      }
    }
  }
  return { codes, filters }
}

// 序列化关联科目为新格式：[{code, filters?}]。
// 以 itemForm.accountCodes 为准，不在选中列表里的科目过滤自动忽略。
function serializeRelatedAccounts(): string | null {
  if (!itemForm.accountCodes.length) return null
  const arr = itemForm.accountCodes.map((code) => {
    const filters = itemForm.accountFilters[code]
    const cleaned = Array.isArray(filters)
      ? filters
          .filter((f) => f && f.auxType && Array.isArray(f.codes) && f.codes.length > 0)
          .map((f) => ({ auxType: f.auxType, codes: [...f.codes] }))
      : []
    return cleaned.length ? { code, filters: cleaned } : { code }
  })
  return JSON.stringify(arr)
}

async function handleSaveItem() {
  if (!selectedItem.value || !selectedTemplateId.value) return
  const trimmedName = itemForm.name.trim()
  if (!trimmedName) { message.warning('请输入项目名称'); return }
  // 改名前记录引用方（后端会同步重写公式引用，前端据此提示）
  const oldName = getItemName(selectedItem.value)
  const renamedRefs = oldName && oldName !== trimmedName ? itemReferences.value.length : 0
  itemSaveLoading.value = true
  try {
    const data: any = {
      itemName: trimmedName,
      nodeRole: itemForm.nodeRole,
      dataSource: itemForm.dataSourceType || null,
      itemCategory: itemForm.itemCategory || null,
      valueSource: itemForm.valueSource || null,
      systemDataSource: itemForm.systemDataSource || null,
      isIndicatorSection: itemForm.isIndicatorSection || false,
      summaryKeywordsJson: itemForm.summaryKeywords.length ? JSON.stringify(itemForm.summaryKeywords) : null,
      relatedAccountsJson: serializeRelatedAccounts(),
      formula: itemForm.formula || null,
      sort: Number(itemForm.sortOrder) || 0,
      unit: itemForm.unit || null,
      perUnitMode: itemForm.perUnitMode || null,
      decimalPlaces: itemForm.decimalPlaces || null,
      dataSourceRemark: itemForm.dataSourceRemark || null,
      calculationLogic: itemForm.calculationLogic || null,
      isManualEntry: ['voucher', 'billing', 'depreciation', 'estimate', 'allocation'].includes(itemForm.dataSourceType) ? false : itemForm.isManualEntry,
      billingFilterJson: itemForm.dataSourceType === 'billing'
        ? JSON.stringify({
            outlets: itemForm.billingFilter.outlets,
            businessObjects: itemForm.billingFilter.businessObjects,
            aggregation: itemForm.billingFilter.aggregation,
            scope: itemForm.billingFilter.scope,
          })
        : null,
    }
    await updateAmoebaPLItem(selectedTemplateId.value, selectedItem.value.id, data)
    message.success('保存成功')
    if (renamedRefs > 0) {
      message.info(`已同步更新 ${renamedRefs} 处公式引用（${oldName} → ${trimmedName}）`)
    }
    itemFormSnapshot.value = getFormSnapshot()
    const savedKeys = [...expandedKeys.value]
    await loadTemplateItems()
    expandedKeys.value = savedKeys
    const updated = flatItems.value.find(i => i.id === selectedItem.value?.id)
    if (updated) doSelectItem(updated)
  } catch (e: any) {
    message.error(e?.message || '保存失败')
  } finally {
    itemSaveLoading.value = false
  }
}

// ==================== 删除 ====================
function handleDeleteItem() {
  if (!selectedItem.value || !selectedTemplateId.value) return
  // 被公式引用时给出明确警示（后端也会拦截，前端提前告知引用方）
  const refs = itemReferences.value
  const refWarning = refs.length > 0
    ? `注意：「${refs.slice(0, 3).map(r => r.name).join('、')}」${refs.length > 3 ? ` 等 ${refs.length} 项` : ''}的公式正在引用该项目，删除前需先调整这些公式。`
    : ''
  Modal.confirm({
    title: '确认删除',
    content: refWarning || `确定要删除损益项「${itemForm.name}」吗？`,
    okType: 'danger',
    async onOk() {
      try {
        await deleteAmoebaPLItem(selectedTemplateId.value!, selectedItem.value.id)
        message.success('删除成功')
        selectedItem.value = null
        editMode.value = null
        const savedKeys = [...expandedKeys.value]
        await loadTemplateItems()
        expandedKeys.value = savedKeys
      } catch (e: any) {
        message.error(e?.message || '删除失败')
      }
    },
  })
}

// ==================== 新增损益项 ====================
const addItemModalVisible = ref(false)
const addItemSubmitLoading = ref(false)
const addItemForm = reactive({
  itemName: '',
  nodeRole: 'data' as string,
  parentId: 0 as number,
  sort: 0,
  dataSource: null as string | null,
  itemCategory: 'revenue' as string,
  valueSource: 'system' as string,
  systemDataSource: null as string | null,
  isIndicatorSection: false as boolean,
})

const addItemSystemDataSourceSelectValue = computed<any>({
  get: () => addItemForm.systemDataSource ?? undefined,
  set: value => { addItemForm.systemDataSource = toStringOrNull(value) },
})

const addItemDataSourceSelectValue = computed<any>({
  get: () => addItemForm.dataSource ?? undefined,
  set: value => { addItemForm.dataSource = toStringOrNull(value) },
})

// 所属位置下拉选项：Tab 节点 + 各级嵌套 group 子节点
const parentOptions = computed<{ value: number; label: string }[]>(() => {
  const options: { value: number; label: string }[] = []
  const tabs = flatItems.value
    .filter(i => (!i.parentId || i.parentId === 0) && getNodeRole(i) === 'group')
    .sort((a, b) => getItemSort(a) - getItemSort(b))

  const appendGroupChildren = (parent: any, prefix: string) => {
    const groups = flatItems.value
      .filter(i => i.parentId === parent.id && getNodeRole(i) === 'group')
      .sort((a, b) => getItemSort(a) - getItemSort(b))
    for (const g of groups) {
      const label = `${prefix} > ${getItemName(g)}`
      options.push({ value: g.id, label })
      appendGroupChildren(g, label)
    }
  }

  for (const tab of tabs) {
    const tabName = getItemName(tab)
    options.push({ value: tab.id, label: tabName })
    appendGroupChildren(tab, tabName)
  }
  return options
})

// 计算指定父节点下的下一个 sortOrder（最大值 + 10）
function computeNextSortOrder(parentId: number): number {
  const siblings = flatItems.value.filter(i => i.parentId === parentId)
  if (!siblings.length) return 10
  const maxSort = Math.max(...siblings.map(i => getItemSort(i)))
  return maxSort + 10
}

async function handleAddItem(parentItem: any) {
  if (!selectedTemplateId.value) { message.warning('请先选择模板'); return }
  // 在固定指标分区Tab下新增：若分区尚未创建，先懒创建并切到真实分区
  if (!parentItem && isIndicatorTabActive.value) {
    const secId = await ensureIndicatorSection()
    if (!secId) return // ensureIndicatorSection 失败时已给提示
    activeTabId.value = secId
  }
  const defaultParentId = parentItem?.id || activeTabId.value || 0
  const underIndicator = checkAncestorIsIndicatorSection(defaultParentId) || (parentItem && isIndicatorSectionNode(parentItem))
  addItemForm.itemName = ''
  addItemForm.parentId = defaultParentId
  addItemForm.sort = computeNextSortOrder(defaultParentId)
  if (underIndicator) {
    // 在指标分区下自动锁定为 indicator
    addItemForm.itemCategory = 'indicator'
    addItemForm.valueSource = 'system'
  } else {
    addItemForm.itemCategory = 'revenue'
    addItemForm.valueSource = 'system'
  }
  addItemForm.systemDataSource = null
  addItemForm.isIndicatorSection = false
  syncAddItemLegacyFields()
  addItemModalVisible.value = true
}

// 懒创建全局唯一指标分区（根级 group + isIndicatorSection），返回其 id；已存在则直接返回。
// 创建失败（如名称重复/网络错误）时与本文件其他处理一致：给出错误提示并返回 null。
async function ensureIndicatorSection(): Promise<number | null> {
  if (indicatorSectionItem.value) return indicatorSectionItem.value.id
  if (!selectedTemplateId.value) return null
  try {
    const created: any = await addAmoebaPLItem(selectedTemplateId.value, {
      itemName: '运营指标',
      nodeRole: 'group',
      parentId: 0,
      sort: computeNextSortOrder(0),
      itemCategory: 'section',
      isIndicatorSection: true,
    })
    const newId = created?.id ?? null
    await loadTemplateItems()
    return newId
  } catch (e: any) {
    message.error(e?.message || '指标分区创建失败')
    return null
  }
}

// 当用户切换所属位置时，自动重算 sort 为该父节点末尾
watch(
  () => addItemForm.parentId,
  (newParentId) => {
    if (!addItemModalVisible.value) return
    addItemForm.sort = computeNextSortOrder(newParentId)
  }
)

async function handleAddItemSubmit() {
  if (!addItemForm.itemName) { message.warning('请输入项目名称'); return }
  if (!addItemForm.itemCategory) { message.warning('请选择项目类别'); return }
  if (!addItemForm.nodeRole) { message.warning('请选择节点角色'); return }
  // 仅指标分区允许创建在根级；普通项落到根级后任何 UI 都不渲染（幽灵项）
  if (!addItemForm.parentId && !addItemForm.isIndicatorSection) {
    message.warning('请选择所属位置')
    return
  }
  if (addItemForm.parentId && !flatItems.value.some(i => i.id === addItemForm.parentId)) {
    message.warning('所属位置已不存在，请重新选择')
    return
  }
  if (addItemForm.isIndicatorSection && hasIndicatorSection.value) {
    message.warning('每个模板只能有一个指标分区')
    return
  }
  addItemSubmitLoading.value = true
  try {
    await addAmoebaPLItem(selectedTemplateId.value!, {
      itemName: addItemForm.itemName.trim(),
      nodeRole: addItemForm.nodeRole,
      parentId: addItemForm.parentId,
      sort: Number(addItemForm.sort) || 0,
      dataSource: addItemForm.nodeRole === 'data' ? addItemForm.dataSource : null,
      itemCategory: addItemForm.itemCategory || null,
      valueSource: addItemForm.valueSource || null,
      systemDataSource: addItemForm.systemDataSource || null,
      isIndicatorSection: addItemForm.isIndicatorSection || false,
    })
    message.success('新增成功')
    addItemModalVisible.value = false
    const savedKeys = [...expandedKeys.value]
    await loadTemplateItems()
    const keysToRestore = new Set(savedKeys)
    if (addItemForm.parentId) keysToRestore.add(`item-${addItemForm.parentId}`)
    expandedKeys.value = [...keysToRestore]
  } catch (e: any) {
    message.error(e?.message || '新增失败')
  } finally {
    addItemSubmitLoading.value = false
  }
}

// ==================== 公式编辑器可选项 ====================
const formulaAvailableItems = computed(() => {
  const currentId = selectedItem.value?.id
  // 编辑全局公式时排除被编辑的全局公式节点自身（防自引用）
  const editingGlobalId = globalFormulaEditVisible.value ? globalFormulaEditingItem.value?.id : null
  return flatItems.value
    // indicator 项名称不参与唯一性校验、后端不作为公式引用目标，插入 ${指标名} 永远解析不到
    .filter(i => getNodeRole(i) !== 'indicator')
    .filter(i => (!currentId || i.id !== currentId) && (!editingGlobalId || i.id !== editingGlobalId))
    .map(i => ({
      id: i.id,
      name: getItemName(i),
      direction: '', // 统一树模型无方向概念
    }))
})

/**
 * 公式文本校验：${} 闭合、引用名称存在于可引用集合、禁止自引用。
 * 返回错误文案；合法返回 null。
 */
function validateFormulaText(formula: string, allowedNames: Set<string>, selfName?: string): string | null {
  if (!formula || !formula.trim()) return null
  const openCount = (formula.match(/\$\{/g) || []).length
  const tokens = [...formula.matchAll(/\$\{([^}]*)\}/g)].map(m => m[1].trim())
  if (openCount !== tokens.length) return '公式存在未闭合的 ${ 引用'
  for (const t of tokens) {
    if (!t) return '公式存在空的 ${} 引用'
    if (selfName && t === selfName) return '公式不能引用自身'
    if (!allowedNames.has(t)) return `公式引用的「${t}」不存在或不可引用（指标项不可作为引用目标）`
  }
  return null
}

// section/group 公式可选项：仅列当前节点的子项
const sectionChildItems = computed(() => {
  if (!selectedItem.value) return []
  const parentId = selectedItem.value.id
  return flatItems.value
    .filter(i => i.parentId === parentId)
    .map(i => ({ id: i.id, name: getItemName(i), direction: '' }))
})

// 公式弹窗当前上下文
const formulaModalContext = ref<'item' | 'section'>('item')
const currentFormulaItems = computed(() =>
  formulaModalContext.value === 'section' ? sectionChildItems.value : formulaAvailableItems.value
)

// ==================== 公式弹窗 ====================
const formulaModalVisible = ref(false)
const tempFormula = ref('')

// 弹窗 body 最大高度与滚动样式，避免内容超出视口高度时溢出页面
const modalScrollBodyStyle = {
  maxHeight: 'calc(80vh - 110px)',
  overflowY: 'auto' as const,
}
const formulaModalBodyStyle = {
  minHeight: '320px',
  maxHeight: 'calc(80vh - 110px)',
  overflowY: 'auto' as const,
}

function openFormulaModal(context: 'item' | 'section' = 'item') {
  formulaModalContext.value = context
  tempFormula.value = itemForm.formula || ''
  formulaModalVisible.value = true
}

function onFormulaModalOk() {
  // 校验闭合/引用存在性/自引用，保存了求值恒 0 的坏公式用户极难排查
  const allowed = new Set(currentFormulaItems.value.map(i => i.name))
  const selfName = selectedItem.value ? getItemName(selectedItem.value).trim() : undefined
  const error = validateFormulaText(tempFormula.value, allowed, selfName)
  if (error) {
    message.warning(error)
    return
  }
  itemForm.formula = tempFormula.value
  formulaModalVisible.value = false
}

function onFormulaModalCancel() {
  formulaModalVisible.value = false
}

// ==================== 科目覆盖率诊断 ====================
const coverageDrawerVisible = ref(false)
const coverageLoading = ref(false)
const coverageReport = ref<any>(null)

const coverageColumns = [
  { title: '科目编码', dataIndex: 'accountCode', key: 'accountCode', width: 100 },
  { title: '科目名称', dataIndex: 'accountName', key: 'accountName', width: 140 },
  { title: '分录数', dataIndex: 'entryCount', key: 'entryCount', width: 60, align: 'right' as const },
  { title: '未匹配金额', dataIndex: 'totalAmount', key: 'totalAmount', width: 120, align: 'right' as const },
]

async function handleCoverageCheck() {
  if (!selectedTemplateId.value) { message.warning('请先选择模板'); return }
  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) { message.warning('请先选择账套'); return }
  const now = new Date()
  const period = `${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}`
  coverageDrawerVisible.value = true
  coverageLoading.value = true
  coverageReport.value = null
  try {
    const res = await getAmoebaCoverageReport(selectedTemplateId.value, period, accountSetId)
    coverageReport.value = res?.data || res
  } catch (e: any) {
    message.error('覆盖率诊断失败：' + (e.message || '未知错误'))
  } finally {
    coverageLoading.value = false
  }
}

const coverageRate = computed<number | null>(() => {
  const r = coverageReport.value?.coverageRate
  return typeof r === 'number' ? r : null
})
const coverageBadgeColor = computed<string>(() => {
  const r = coverageRate.value
  if (r === null) return 'default'
  if (r >= 95) return 'success'
  if (r >= 80) return 'warning'
  return 'error'
})

// ==================== 科目选项 ====================
const accountOptions = ref<{ value: string; label: string }[]>([])

async function loadAccountOptions() {
  const asId = accountSetStore.getCurrentAccountSetId()
  if (!asId) {
    accountOptions.value = []
    return
  }
  try {
    const tree = await getAccountTree(undefined, asId)
    // 响应返回时账套可能已切换，丢弃过期结果
    if (asId !== accountSetStore.getCurrentAccountSetId()) return
    const flatList: { code: string; label: string }[] = []
    function flattenTree(nodes: any[], ancestorNames: string[] = []) {
      for (const node of nodes) {
        const code = node.code || node.fCode
        const name = node.name || node.fName
        const pathNames = [...ancestorNames, name]
        const label = [code, ...pathNames].join('-')
        flatList.push({ code, label })
        if (node.children?.length) {
          flattenTree(node.children, pathNames)
        }
      }
    }
    flattenTree(Array.isArray(tree) ? tree : [])
    accountOptions.value = flatList.map(a => ({ value: a.code, label: a.label }))
  } catch (e) {
    console.error('加载科目列表失败', e)
    // 失败时清空而非保留上一账套的科目，避免选错科目存进当前模板
    accountOptions.value = []
  }
}

// ==================== 工具函数 ====================
function dataSourceColor(val: string): string {
  if (!val) return 'default'
  if (val.startsWith('voucher')) return 'blue'
  const colorMap: Record<string, string> = {
    billing: 'green', depreciation: 'orange', estimate: 'cyan',
    formula: 'purple', allocation: 'magenta',
  }
  return colorMap[val] || 'default'
}

function dataSourceLabel(val: string) {
  const map: Record<string, string> = {
    billing: '计费', voucher: '凭证', depreciation: '资产折旧',
    estimate: '暂估', allocation: '分摊', formula: '公式',
  }
  if (map[val]) return map[val]
  if (val && val.startsWith('voucher')) return '凭证'
  return val || ''
}

// ==================== 引用关系 ====================
const itemReferences = computed(() => {
  if (!selectedItem.value) return []
  const target = getItemName(selectedItem.value).trim()
  if (!target) return []
  // 用 ${名称} 完整 token 匹配：裸子串会把"出港收入合计"误判为引用"出港收入"
  const token = '${' + target + '}'
  return flatItems.value
    .filter(i => {
      if (i.id === selectedItem.value?.id) return false
      const f = i.formula || i.fFormula || ''
      return typeof f === 'string' && f.includes(token)
    })
    .map(i => ({ id: i.id, name: getItemName(i), direction: '' }))
})

// ==================== 复制到其他模板 ====================
const cloneTargetVisible = ref(false)
const cloneTargetTemplateId = ref<number | null>(null)
const cloneTargetParentId = ref<number | null>(null)
const cloneTargetParentLoading = ref(false)
const cloneTargetParentOptions = ref<{ value: number; label: string }[]>([])
const cloneSubmitting = ref(false)
const cloneTargetTemplateSelectValue = computed<any>({
  get: () => cloneTargetTemplateId.value ?? undefined,
  set: value => { cloneTargetTemplateId.value = toNumberOrNull(value) },
})
const cloneTargetParentSelectValue = computed<any>({
  get: () => cloneTargetParentId.value ?? undefined,
  set: value => { cloneTargetParentId.value = toNumberOrNull(value) },
})
const cloneTargetOptions = computed(() =>
  templates.value
    .filter(t => t.id !== selectedTemplateId.value)
    .map(t => ({ value: t.id, label: t.name || t.fName }))
)
// 板块/全局公式可以落在目标模板根级；普通项必须挂在某个分组下，否则不可见
const cloneNeedsParent = computed(() => {
  const role = selectedItem.value ? getNodeRole(selectedItem.value) : ''
  return role !== 'group' && role !== 'formula'
})

// 选择目标模板后加载其分组树，供"目标位置"选择（默认第一个板块）
let cloneParentLoadSeq = 0
watch(cloneTargetTemplateId, async (templateId) => {
  const seq = ++cloneParentLoadSeq
  cloneTargetParentId.value = null
  cloneTargetParentOptions.value = []
  if (!templateId || !cloneNeedsParent.value) return
  cloneTargetParentLoading.value = true
  try {
    const res = await getAmoebaPLTemplateById(templateId)
    if (seq !== cloneParentLoadSeq) return // 期间已切换目标模板，丢弃过期响应
    const items = res?.items || res?.children || []
    const groups: { value: number; label: string }[] = []
    const collectGroups = (nodes: any[], prefix: string) => {
      for (const node of nodes || []) {
        if (getNodeRole(node) !== 'group') continue
        const label = prefix ? `${prefix} / ${getItemName(node)}` : getItemName(node)
        // 指标分区下只能放指标项，普通项复制不提供该入口
        if (!node.isIndicatorSection) {
          groups.push({ value: node.id, label })
          collectGroups(node.children || [], label)
        }
      }
    }
    collectGroups(items, '')
    cloneTargetParentOptions.value = groups
    cloneTargetParentId.value = groups[0]?.value ?? null
  } catch {
    // 加载失败时保持为空，提交前有校验兜底
  } finally {
    if (seq === cloneParentLoadSeq) cloneTargetParentLoading.value = false
  }
})

function handleCloneCurrentItem() {
  if (!selectedItem.value) { message.info('请先选中要复制的项目'); return }
  if (templates.value.length <= 1) { message.warning('当前账套下只有1个模板，无可选目标'); return }
  cloneTargetTemplateId.value = null
  cloneTargetParentId.value = null
  cloneTargetParentOptions.value = []
  cloneTargetVisible.value = true
}
async function handleConfirmClone() {
  if (!selectedItem.value || !selectedTemplateId.value || !cloneTargetTemplateId.value) {
    message.warning('请选择目标模板'); return
  }
  if (cloneNeedsParent.value && !cloneTargetParentId.value) {
    message.warning('请选择目标位置（目标模板内的分组）'); return
  }
  cloneSubmitting.value = true
  try {
    await cloneItemFromTemplate(cloneTargetTemplateId.value, {
      sourceTemplateId: selectedTemplateId.value,
      sourceItemId: selectedItem.value.id,
      targetParentId: cloneNeedsParent.value ? cloneTargetParentId.value : null,
      cloneChildren: true,
    })
    message.success('已复制到目标模板')
    cloneTargetVisible.value = false
  } catch (e: any) {
    message.error('复制失败：' + (e?.message || ''))
  } finally {
    cloneSubmitting.value = false
  }
}

// ==================== 路由守卫 & 快捷键 ====================
useUnsavedGuard({
  isDirty: isFormDirty,
  routerLeaveMessage: '当前项目有未保存的修改，离开将丢失，是否继续？',
  onDiscard: () => {
    if (selectedItem.value) fillItemForm(selectedItem.value)
  },
})

const { helpVisible: shortcutHelpVisible } = useTemplateShortcuts({
  onSave: () => {
    if (editMode.value === 'item' && isFormDirty.value && !itemSaveLoading.value) handleSaveItem()
  },
  onSearch: () => {
    nextTick(() => {
      const el = document.querySelector('.left-panel input') as HTMLInputElement | null
      el?.focus()
    })
  },
  onCopy: () => handleCloneCurrentItem(),
  onEscape: () => requestDeselect(),
})

// ==================== 生命周期 ====================
onMounted(() => {
  loadTemplates()
  loadAccountOptions()
  loadAuxTypes()
})
</script>

<style scoped lang="scss">
.page-container {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}

.main-content {
  display: flex;
  flex: 1;
  min-height: 0;
  height: 100%;
}

.left-panel {
  min-width: 240px;
  max-width: 600px;
  overflow-y: auto;
  padding: 16px;
  background: var(--bg-muted);
  border-right: 1px solid var(--border);
}

.panel-resizer {
  width: 8px;
  cursor: col-resize;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-muted);
  flex-shrink: 0;
  transition: background 0.2s;

  &:hover, &:active { background: var(--border); }

  .resizer-line {
    width: 2px;
    height: 32px;
    border-radius: 1px;
    background: var(--border);
    transition: background 0.2s;
  }

  &:hover .resizer-line, &:active .resizer-line { background: var(--color-primary); }
}

.right-panel {
  flex: 1;
  min-width: 0;
  overflow-y: auto;
  padding: 16px;
  background: var(--bg-card);
}

.tree-toolbar {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
  padding: 0 4px;
}

.tree-node-title {
  display: flex;
  align-items: center;
  gap: 6px;
}

.tree-node-group {
  font-weight: 600;
  color: var(--text-1);
}

// Tab 横跨容器
.tab-bar {
  background: var(--bg-card);
  padding: 4px 12px 0;
  margin-bottom: 0;
  border-bottom: 1px solid var(--border);
}

.tabs-and-formulas {
  display: flex;
  align-items: flex-end;
  gap: 16px;
}

.tab-tabs {
  flex: 1;
  min-width: 0;

  :deep(.ant-tabs-nav) { margin: 0 !important; }
  :deep(.ant-tabs-nav::before) { border-bottom: none !important; }
  :deep(.ant-tabs-content-holder) { display: none; }
  :deep(.ant-tabs-tab) {
    padding: 6px 16px !important;
    font-size: 14px;
    font-weight: 500;
    background: var(--bg-muted) !important;
    border: 1px solid var(--border) !important;
    border-bottom: none !important;
    border-radius: 6px 6px 0 0 !important;
    color: var(--text-2);
    transition: background 0.2s, color 0.2s, box-shadow 0.2s;
  }
  :deep(.ant-tabs-tab:hover) {
    background: var(--color-info-light) !important;
    color: var(--color-primary) !important;
  }
  :deep(.ant-tabs-tab.ant-tabs-tab-active) {
    background: var(--color-primary-light) !important;
    border-color: var(--color-primary-border) !important;
    box-shadow: inset 0 3px 0 0 var(--color-primary);
  }
  :deep(.ant-tabs-tab.ant-tabs-tab-active .ant-tabs-tab-btn) {
    color: var(--color-primary) !important;
    font-weight: 600;
  }
}

.dir-tab-label {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.indicator-tab {
  font-weight: 600;
  color: var(--color-warning); // 琥珀色，与报表"运营指标"呼应
  .anticon {
    margin-right: 2px;
  }
}

.indicator-tab--drop-active {
  outline: 2px dashed var(--color-success);
  outline-offset: 2px;
  border-radius: 4px;
  background: color-mix(in srgb, var(--color-success) 8%, transparent);
}

.dir-tab-count {
  color: var(--text-3);
  font-size: 12px;
}

.dir-tab-edit {
  color: var(--text-3);
  font-size: 12px;
  margin-left: 2px;
  cursor: pointer;
  &:hover { color: var(--color-primary); }
}

.dir-tab-del {
  color: var(--text-3);
  font-size: 11px;
  margin-left: 2px;
  cursor: pointer;
  &:hover { color: var(--color-danger); }
}

// 全局formula区域
.global-formula-area {
  display: flex;
  align-items: center;
  gap: 8px;
  padding-bottom: 6px;
  flex-shrink: 0;
}

.global-formula-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 3px 10px;
  background: var(--bg-card);
  border: 1px solid var(--color-primary);
  border-radius: 16px;
  font-size: 13px;
  color: var(--color-primary);
  cursor: pointer;
  transition: all 0.2s;

  &:hover { background: var(--color-primary-light); }

  &__name { font-weight: 500; white-space: nowrap; }
  &__fx {
    font-size: 10px;
    background: var(--color-primary);
    color: var(--text-on-accent);
    padding: 0 4px;
    border-radius: 3px;
    font-style: italic;
    line-height: 16px;
  }
}

// 右栏
.edit-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--border);
}

.edit-title {
  font-weight: 600;
  font-size: 15px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.dirty-dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--color-danger);
}

.edit-form { max-width: 100%; }

.form-grid-2col {
  display: grid;
  grid-template-columns: 1fr 1.4fr;
  gap: 0 16px;
}

.formula-preview {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-radius: 4px;
  min-height: 32px;
}

.formula-preview__text {
  flex: 1;
  font-family: 'Consolas', monospace;
  font-size: 13px;
  color: var(--text-1);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.formula-preview__empty {
  flex: 1;
  color: var(--text-3);
  font-size: 13px;
}

.field-help-icon {
  color: var(--text-3);
  margin-left: 4px;
  cursor: help;
  transition: color 0.2s;
  &:hover { color: var(--color-primary); }
}

.empty-guide {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 80px 24px;
  text-align: center;

  &__icon { font-size: 48px; margin-bottom: 16px; }
  &__title { font-size: 16px; font-weight: 500; color: var(--text-1); margin-bottom: 8px; }
  &__desc { font-size: 13px; color: var(--text-3); max-width: 280px; line-height: 1.6; }
}

.toolbar-divider {
  display: inline-block;
  width: 1px;
  height: 18px;
  background: var(--border);
  margin: 0 4px;
  align-self: center;
}

.coverage-btn {
  position: relative;
  &:hover { color: var(--color-primary); }
}

// 辅助核算过滤
.aux-filter {
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 8px;
  background: var(--bg-muted);

  &__empty { font-size: 13px; color: var(--text-3); text-align: center; padding: 8px 0; }
  &__row { display: flex; gap: 8px; align-items: center; margin-bottom: 6px; }
}

// 科目级辅助过滤列表
.account-filters {
  display: flex;
  flex-direction: column;
  gap: 6px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-muted);
  padding: 6px;

  &__item {
    background: var(--bg-card);
    border: 1px solid var(--border);
    border-radius: 4px;
    overflow: hidden;
  }

  &__head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    padding: 6px 10px;
    cursor: pointer;
    user-select: none;
    transition: background 0.15s;

    &:hover { background: var(--color-info-light); }
  }

  &__title {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    font-size: 13px;
    color: var(--text-1);
    flex: 1;
    min-width: 0;
    overflow: hidden;
  }

  &__code {
    font-family: 'Consolas', monospace;
    font-weight: 600;
    color: var(--color-info);
  }

  &__name {
    color: var(--text-2);
    font-size: 12px;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }

  &__badge {
    flex-shrink: 0;
    font-size: 12px;
    color: var(--text-3);
    padding: 0 8px;
    line-height: 20px;
    border-radius: 10px;
    background: var(--border);

    &--active {
      color: var(--text-on-accent);
      background: var(--color-success);
    }
  }

  &__body {
    padding: 8px 10px 10px;
    border-top: 1px dashed var(--border);
    background: var(--bg-muted);
  }

  &__empty-tip {
    font-size: 12px;
    color: var(--text-3);
    text-align: center;
    padding: 4px 0 8px;
  }
}

// ItemEditor 卡片分组
.editor-card {
  position: relative;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 14px 16px 6px 18px;
  margin-bottom: 16px;
  box-shadow: var(--shadow-sm);
  transition: box-shadow 0.2s, border-color 0.2s;

  &::before {
    content: '';
    position: absolute;
    left: 0; top: 0; bottom: 0;
    width: 4px;
    background: var(--border-strong);
    border-radius: 6px 0 0 6px;
  }

  &:hover { box-shadow: var(--shadow-md); }

  &__head {
    display: flex;
    align-items: baseline;
    gap: 10px;
    margin-bottom: 14px;
    padding-bottom: 10px;
    border-bottom: 1px solid var(--border);
  }

  &__title { font-size: 15px; font-weight: 600; color: var(--text-1); letter-spacing: 0.2px; }
  &__hint { font-size: 12px; color: var(--text-3); }

  :deep(.ant-form-item) { margin-bottom: 12px; }

  &--basic {
    &::before { background: var(--border-strong); }
    .editor-card__head { background: linear-gradient(to right, color-mix(in srgb, var(--border-strong) 6%, transparent), transparent 60%); margin: -14px -16px 14px -18px; padding: 14px 16px 10px 18px; border-bottom: 1px solid var(--border); }
  }
  &--source {
    &::before { background: var(--color-info); }
    .editor-card__head { background: linear-gradient(to right, color-mix(in srgb, var(--color-info) 8%, transparent), transparent 60%); margin: -14px -16px 14px -18px; padding: 14px 16px 10px 18px; border-bottom: 1px solid var(--border); }
    .editor-card__title { color: var(--color-info-text); }
  }
  &--rule {
    &::before { background: var(--color-success); }
    .editor-card__head { background: linear-gradient(to right, color-mix(in srgb, var(--color-success) 8%, transparent), transparent 60%); margin: -14px -16px 14px -18px; padding: 14px 16px 10px 18px; border-bottom: 1px solid var(--border); }
    .editor-card__title { color: var(--color-success-text); }
  }
  &--calc {
    &::before { background: var(--color-primary); }
    .editor-card__head { background: linear-gradient(to right, color-mix(in srgb, var(--color-primary) 8%, transparent), transparent 60%); margin: -14px -16px 14px -18px; padding: 14px 16px 10px 18px; border-bottom: 1px solid var(--border); }
    .editor-card__title { color: var(--color-primary-active); }
  }
  &--remark {
    &::before { background: var(--color-warning); }
    .editor-card__head { background: linear-gradient(to right, color-mix(in srgb, var(--color-warning) 7%, transparent), transparent 60%); margin: -14px -16px 14px -18px; padding: 14px 16px 10px 18px; border-bottom: 1px solid var(--border); }
    .editor-card__title { color: var(--color-warning); }
  }
}

// 底部 ActionBar
.action-bar {
  position: sticky;
  bottom: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 24px;
  background: var(--color-warning-light);
  border-top: 1px solid var(--color-warning-border);
  box-shadow: 0 -2px 8px rgba(0, 0, 0, 0.06);

  &__hint {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    font-size: 13px;
    color: var(--text-2);
  }
  &__dot {
    display: inline-block;
    width: 8px; height: 8px;
    border-radius: 50%;
    background: var(--color-warning);
    box-shadow: 0 0 0 3px rgba(250, 173, 20, 0.16);
    animation: action-bar-pulse 1.6s infinite;
  }
}

@keyframes action-bar-pulse {
  0%, 100% { box-shadow: 0 0 0 3px rgba(250, 173, 20, 0.16); }
  50%      { box-shadow: 0 0 0 6px rgba(250, 173, 20, 0.04); }
}

.action-bar-fade-enter-active,
.action-bar-fade-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}
.action-bar-fade-enter-from,
.action-bar-fade-leave-to {
  opacity: 0;
  transform: translateY(8px);
}

.shortcut-list {
  list-style: none;
  margin: 0; padding: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 13px;
  color: var(--text-2);

  li {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 6px 0;
    border-bottom: 1px dashed var(--border);
  }
  li:last-child { border-bottom: none; }
  span { color: var(--text-3); font-size: 12px; }
}

kbd {
  display: inline-block;
  padding: 1px 6px;
  margin: 0 2px;
  font-family: 'Consolas', monospace;
  font-size: 12px;
  color: var(--text-1);
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-bottom-width: 2px;
  border-radius: 3px;
  min-width: 18px;
  text-align: center;
}

@media (max-width: 1280px) {
  .main-content > :deep(.amoeba-helper-panel:not(.amoeba-helper-panel--collapsed)) {
    width: 240px !important;
  }
}
@media (max-width: 1024px) {
  .main-content > :deep(.amoeba-helper-panel) { display: none; }
}
</style>
