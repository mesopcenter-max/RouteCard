document.addEventListener('DOMContentLoaded', () => {
  const tabs = Array.from(document.querySelectorAll('[role="tab"]'));
  const panels = tabs.map(tab => document.getElementById(tab.getAttribute('aria-controls'))).filter(Boolean);

  function activateTab(tab, moveFocus = false) {
    tabs.forEach(candidate => {
      const selected = candidate === tab;
      candidate.classList.toggle('active', selected);
      candidate.setAttribute('aria-selected', String(selected));
      candidate.tabIndex = selected ? 0 : -1;
    });
    panels.forEach(panel => {
      const selected = panel.id === tab.getAttribute('aria-controls');
      panel.classList.toggle('active', selected);
      panel.setAttribute('aria-hidden', String(!selected));
    });
    if (moveFocus) tab.focus();
  }

  tabs.forEach((tab, index) => {
    tab.addEventListener('click', () => activateTab(tab));
    tab.addEventListener('keydown', event => {
      let nextIndex = index;
      if (event.key === 'ArrowRight') nextIndex = (index + 1) % tabs.length;
      else if (event.key === 'ArrowLeft') nextIndex = (index - 1 + tabs.length) % tabs.length;
      else if (event.key === 'Home') nextIndex = 0;
      else if (event.key === 'End') nextIndex = tabs.length - 1;
      else return;
      event.preventDefault();
      activateTab(tabs[nextIndex], true);
    });
  });
  if (tabs.length) activateTab(tabs[0]);

  document.querySelector('#print-route-card')?.addEventListener('click', () => window.print());

  const routeForm = document.querySelector('.route-form');
  const saveStatus = document.querySelector('#save-route-card-status');
  const saveButton = document.querySelector('#save-route-card');
  const cardFieldNames = new Set([
    'routeCardNo', 'orderNo', 'qty', 'workOrderNo', 'jcQty', 'partNo', 'partName', 'pslNo',
    'halDrawingNo', 'issueNo', 'partsPerUnit', 'referenceDrawingNo', 'materialSpecification',
    'materialIssue', 'materialDescription', 'mrNo', 'materialDate', 'materialType', 'receivedCondition',
    'rawMaterialDia', 'rawMaterialLength', 'rawMaterialBatchNo', 'alternativeMaterialSize',
    'rubberSpecification', 'rubberMixDate', 'rubberExpiryDate', 'rubberQty', 'rubberSupplier', 'taPcRef',
    'bsWeight', 'forgingWeight', 'forgingPlant', 'forgingBatchNo', 'forgingQty', 'rubberBatchNo',
    'rubberLtrNo', 'unFett', 'fettWeight', 'castingBatchNo', 'castingQty', 'padWeight',
    'platingBatchNo', 'mixBatchNo', 'powderQty'
  ]);

  function getFieldValue(field) {
    if (!field.value) return null;
    return field.type === 'number' ? Number(field.value) : field.value;
  }

  function getOperationRows() {
    return Array.from(operationsTable?.tBodies[0].rows || []).map(row => {
      const operation = {};
      row.querySelectorAll('[name]').forEach(field => {
        const match = field.name.match(/^operation(No|Description|DepWcr|AccQty|RejQty|Stamp|Sign)\d+$/);
        if (match) {
          const property = match[1][0].toLowerCase() + match[1].slice(1);
          operation[property] = getFieldValue(field);
        }
      });
      return operation;
    }).filter(operation => Object.values(operation).some(value => value !== null));
  }

  routeForm?.addEventListener('submit', async event => {
    event.preventDefault();
    if (saveButton) saveButton.disabled = true;
    if (saveStatus) saveStatus.textContent = 'Saving route card...';

    const card = {};
    const traceability = [];
    Array.from(routeForm.elements).forEach(field => {
      if (!field.name) return;
      const value = getFieldValue(field);
      if (cardFieldNames.has(field.name)) {
        card[field.name] = value;
      } else if (!/^operation(No|Description|DepWcr|AccQty|RejQty|Stamp|Sign)\d+$/.test(field.name)) {
        traceability.push({ field: field.name, value });
      }
    });

    try {
      const response = await fetch('/api/route-cards', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ card, traceability, operations: getOperationRows() })
      });
      let result;
      try {
        result = await response.json();
      } catch {
        throw new Error(`The server returned HTTP ${response.status}.`);
      }
      if (!response.ok) throw new Error(result.detail || result.error || 'The route card could not be saved.');
      if (saveStatus) saveStatus.textContent = `Route card saved successfully (record ${result.id}).`;
    } catch (error) {
      if (saveStatus) saveStatus.textContent = `Save failed: ${error.message}`;
    } finally {
      if (saveButton) saveButton.disabled = false;
    }
  });

  function nextRowNumber(tbody, pattern) {
    return Array.from(tbody.querySelectorAll('input[name]')).reduce((highest, input) => {
      const match = input.name.match(pattern);
      return match ? Math.max(highest, Number(match[1])) : highest;
    }, 0) + 1;
  }

  function addInputCell(row, name, label, type = 'text', value = '') {
    const cell = document.createElement('td');
    const isDescription = name.startsWith('operationDescription');
    const input = isDescription ? document.createElement('textarea') : document.createElement('input');
    input.name = name;
    input.value = value ?? '';
    input.setAttribute('aria-label', label);
    if (!isDescription) {
      input.type = type;
      if (type === 'number') input.min = '0';
    }
    cell.append(input);
    row.append(cell);
  }

  function addDeleteButton(row, label) {
    const cell = document.createElement('td');
    cell.className = 'delete-cell';
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'row-delete-button';
    button.textContent = 'Delete';
    button.setAttribute('aria-label', label);
    button.addEventListener('click', () => row.remove());
    cell.append(button);
    row.append(cell);
  }

  const operationsTable = document.querySelector('.operations-table');
  if (operationsTable) {
    const heading = document.createElement('th');
    heading.className = 'actions-heading';
    heading.setAttribute('aria-label', 'Row actions');
    operationsTable.tHead.rows[0].append(heading);
    operationsTable.tBodies[0].querySelectorAll('tr').forEach((row, index) => addDeleteButton(row, `Delete row ${index + 1}`));
  }

  document.querySelectorAll('[data-add-row="operations"]').forEach(button => {
    button.addEventListener('click', () => {
      if (!operationsTable) return;
      const tbody = operationsTable.tBodies[0];
      const row = document.createElement('tr');
      const rowNumber = nextRowNumber(tbody, /^operationNo(\d+)$/);
      const displayNumber = tbody.rows.length + 1;
      const fields = [
        ['operationNo', 'operation number', 'text'],
        ['operationDescription', 'operation description', 'text'],
        ['operationDepWcr', 'department or WCR', 'text'],
        ['operationAccQty', 'accepted quantity', 'number'],
        ['operationRejQty', 'rejected quantity', 'number'],
        ['operationStamp', 'stamp', 'text'],
        ['operationSign', 'signature', 'text']
      ];
      fields.forEach(([name, label, type]) => addInputCell(row, `${name}${rowNumber}`, `Operation ${displayNumber} ${label}`, type));
      addDeleteButton(row, `Delete row ${displayNumber}`);
      tbody.append(row);
    });
  });

  function setField(name, value) {
    const field = document.querySelector(`[name="${name}"]`);
    if (field && value !== null && value !== undefined) field.value = value;
  }

  function populateOperations(operations) {
    if (!operationsTable || !Array.isArray(operations) || operations.length === 0) return;
    const tbody = operationsTable.tBodies[0];
    tbody.innerHTML = '';
    operations.forEach((operation, index) => {
      const row = document.createElement('tr');
      const rowNumber = index + 1;
      addInputCell(row, `operationNo${rowNumber}`, `Operation ${rowNumber} number`, 'text', operation.opNo);
      addInputCell(row, `operationDescription${rowNumber}`, `Operation ${rowNumber} description`, 'text', operation.operationName);
      addInputCell(row, `operationDepWcr${rowNumber}`, `Operation ${rowNumber} department or WCR`, 'text', operation.workcenterDepartment);
      addInputCell(row, `operationAccQty${rowNumber}`, `Operation ${rowNumber} accepted quantity`, 'number');
      addInputCell(row, `operationRejQty${rowNumber}`, `Operation ${rowNumber} rejected quantity`, 'number');
      addInputCell(row, `operationStamp${rowNumber}`, `Operation ${rowNumber} stamp`);
      addInputCell(row, `operationSign${rowNumber}`, `Operation ${rowNumber} signature`);
      addDeleteButton(row, `Delete row ${rowNumber}`);
      tbody.append(row);
    });
  }

  const fieldAliases = {
    PART_NO: 'partNo', PARTNO: 'partNo', ITEM_NO: 'partNo', MATERIAL_NO: 'partNo',
    PART_NAME: 'partName', PARTNAME: 'partName', ITEM_NAME: 'partName', ITEM_DESC: 'partName', DESCRIPTION: 'partName', PART_DESCRIPTION: 'partName',
    PSL_NO: 'pslNo', PSLNO: 'pslNo', PSL: 'pslNo',
    ORDER_NO: 'orderNo', SHOP_ORDER_NO: 'orderNo', ORDER_NUMBER: 'orderNo', ORDERNUMBER: 'orderNo',
    WORK_ORDER_NO: 'workOrderNo', WORKORDER_NO: 'workOrderNo', WORK_ORDER: 'workOrderNo', WORKORDER: 'workOrderNo',
    SPLIT_QTY: 'jcQty', SPLIT_QUANTITY: 'jcQty', JC_QTY: 'jcQty', JOB_QTY: 'jcQty',
    QTY: 'qty', QUANTITY: 'qty', ORDER_QTY: 'qty', SHOP_ORDER_QTY: 'qty',
    HAL_DRG_NO: 'halDrawingNo', HAL_DRAWING_NO: 'halDrawingNo', ISSUE_NO: 'issueNo',
    MATERIAL_SPECIFICATION: 'materialSpecification', MATERIAL_ISSUE: 'materialIssue', MATERIAL_DESCRIPTION: 'materialDescription',
    MR_NO: 'mrNo', MATERIAL_TYPE: 'materialType', RECEIVED_CONDITION: 'receivedCondition',
    RAW_MATERIAL_DIA: 'rawMaterialDia', RAW_MATERIAL_LENGTH: 'rawMaterialLength', RAW_MATERIAL_BATCH_NO: 'rawMaterialBatchNo',
    ALTERNATIVE_MATERIAL_SIZE: 'alternativeMaterialSize', RUBBER_SPECIFICATION: 'rubberSpecification', RUBBER_QTY: 'rubberQty', RUBBER_SUPPLIER: 'rubberSupplier',
    TA_PC_REF: 'taPcRef', BS_WEIGHT: 'bsWeight', FORGING_WEIGHT: 'forgingWeight', FORGING_PLANT: 'forgingPlant', FORGING_BATCH_NO: 'forgingBatchNo', FORGING_QTY: 'forgingQty',
    RUBBER_BATCH_NO: 'rubberBatchNo', RUBBER_LTR_NO: 'rubberLtrNo', UN_FETT: 'unFett', FETT_WEIGHT: 'fettWeight', CASTING_BATCH_NO: 'castingBatchNo', CASTING_QTY: 'castingQty',
    PAD_WEIGHT: 'padWeight', PLATING_BATCH_NO: 'platingBatchNo', MIX_BATCH_NO: 'mixBatchNo', POWDER_QTY: 'powderQty'
  };

  function setMatchedFields(fields) {
    Object.entries(fields || {}).forEach(([name, value]) => {
      const target = fieldAliases[name.toUpperCase()] || name;
      setField(target, value);
    });
  }

  async function searchOrder() {
    const orderNo = document.querySelector('[name="orderNo"]')?.value.trim();
    const status = document.querySelector('#order-search-status');
    const button = document.querySelector('#search-order-button');
    if (!orderNo) {
      if (status) status.textContent = 'Enter an order number.';
      return;
    }

    if (button) button.disabled = true;
    if (status) status.textContent = 'Searching...';
    try {
      const response = await fetch(`/api/route-cards/lookup?orderNo=${encodeURIComponent(orderNo)}`);
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || 'No matching data was found.');

      setField('orderNo', data.orderNo || orderNo);
      setField('workOrderNo', data.workOrderNo);
      setField('partNo', data.partNo);
      setField('partName', data.partName);
      setField('pslNo', data.pslNo);
      setField('qty', data.qty);
      setField('jcQty', data.jcQty ?? data.qty);
      setField('operationPslNo', data.pslNo);
      setField('operationWorkOrderNo', data.workOrderNo);
      setField('operationJcQty', data.jcQty ?? data.qty);
      setMatchedFields(data.matchedFields);
      populateOperations(data.operations);
      if (status) status.textContent = `Loaded data from ${data.sourceTable || 'Opcenter'}.`;
    } catch (error) {
      if (status) status.textContent = error.message;
    } finally {
      if (button) button.disabled = false;
    }
  }

  async function searchPart() {
    const partNo = document.querySelector('[name="partNo"]')?.value.trim();
    const status = document.querySelector('#part-search-status');
    const button = document.querySelector('#search-part-button');
    if (!partNo) {
      if (status) status.textContent = 'Enter a part number.';
      return;
    }

    if (button) button.disabled = true;
    if (status) status.textContent = 'Searching...';
    try {
      const response = await fetch(`/api/route-cards/lookup?partNo=${encodeURIComponent(partNo)}`);
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || 'No matching data was found.');

      setField('partNo', data.partNo || partNo);
      setField('partName', data.partName);
      setField('pslNo', data.pslNo);
      setField('orderNo', data.orderNo);
      setField('workOrderNo', data.workOrderNo);
      setField('qty', data.qty);
      setField('jcQty', data.jcQty ?? data.qty);
      setField('operationPslNo', data.pslNo);
      setField('operationWorkOrderNo', data.workOrderNo);
      setField('operationJcQty', data.jcQty ?? data.qty);
      setMatchedFields(data.matchedFields);
      populateOperations(data.operations);
      if (status) status.textContent = `Loaded ${data.records?.length || 0} matching rows and ${data.operations?.length || 0} operations from ${(data.sourceDatabases || []).filter((value, index, values) => values.indexOf(value) === index).join(', ') || 'HALFNF/HALNEW'}.`;
    } catch (error) {
      if (status) status.textContent = error.message;
    } finally {
      if (button) button.disabled = false;
    }
  }

  document.querySelector('#search-part-button')?.addEventListener('click', searchPart);
  document.querySelector('[name="partNo"]')?.addEventListener('keydown', event => {
    if (event.key === 'Enter') {
      event.preventDefault();
      searchPart();
    }
  });
});