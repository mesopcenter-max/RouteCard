document.addEventListener('DOMContentLoaded', () => {
  const routeForm = document.querySelector('.route-form');
  const saveStatus = document.querySelector('#save-route-card-status');
  const saveButton = document.querySelector('#save-route-card');
  const operationsTable = document.querySelector('.operations-table');
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
  let userStamps = [];

  function getFieldValue(field, nullByDefault = false) {
    if (!field.value) return nullByDefault ? null : undefined;
    return field.type === 'number' ? Number(field.value) : field.value;
  }

  function getOperationRows() {
    return Array.from(operationsTable?.tBodies[0].rows || []).map(row => {
      const operation = {};
      row.querySelectorAll('[name]').forEach(field => {
        const match = field.name.match(/^operation(No|Description|DepWcr|AccQty|RejQty|Stamp|Sign|ApprovedAt)\d+$/);
        if (match) {
          const property = match[1][0].toLowerCase() + match[1].slice(1);
          operation[property] = getFieldValue(field);
        }
      });
      return operation;
    }).filter(operation => Object.values(operation).some(value => value !== null && value !== undefined && value !== ''));
  }

  function getApprovals() {
    const operationApprovals = Array.from(operationsTable?.tBodies[0].rows || []).map(row => ({
      operationNo: row.querySelector('[name^="operationNo"]')?.value.trim(),
      stampId: Number(row.querySelector('[name^="operationStamp"]')?.value),
      approvedAt: row.querySelector('[name^="operationApprovedAt"]')?.value
    })).filter(approval => approval.operationNo && approval.stampId && approval.approvedAt);
    const cardApprovals = Array.from(document.querySelectorAll('[data-card-stamp-select]')).map(select => {
      const key = select.dataset.approvalKey;
      return {
        operationNo: select.dataset.approvalName,
        stampId: Number(select.value),
        approvedAt: document.querySelector(`[name="${key}ApprovedAt"]`)?.value
      };
    }).filter(approval => approval.operationNo && approval.stampId && approval.approvedAt);
    return [...operationApprovals, ...cardApprovals];
  }

  function updateApproval(row) {
    const stamp = row?.querySelector('[data-stamp-select]');
    const date = row?.querySelector('[name^="operationApprovedAt"]');
    const signature = row?.querySelector('[name^="operationSign"]');
    if (!stamp || !date || !signature || !date.value || !stamp.value) return;
    const option = stamp.selectedOptions[0];
    const baseStamp = option.dataset.base || option.textContent.split(' ')[0];
    const baseSignature = option.dataset.signature || '';
    option.dataset.base = baseStamp;
    option.textContent = `${baseStamp} - ${date.value}`;
    signature.value = `${baseSignature} - ${date.value}`;
    signature.dataset.baseSignature = baseSignature;
    row.classList.add('operation-approved');
  }

  function ensureApprovalPrintFields(row) {
    const stampCell = row.querySelector('[data-stamp-select]')?.closest('td');
    const signatureCell = row.querySelector('[name^="operationSign"]')?.closest('td');
    if (stampCell && !stampCell.querySelector('.print-approval-stamp')) {
      const stampGraphic = document.createElement('div');
      stampGraphic.className = 'print-approval-stamp';
      stampGraphic.setAttribute('aria-hidden', 'true');
      const stampDate = document.createElement('small');
      stampDate.className = 'print-approval-date';
      stampDate.setAttribute('aria-hidden', 'true');
      stampCell.append(stampGraphic, stampDate);
    }
    if (signatureCell && !signatureCell.querySelector('.print-approval-signature')) {
      const signatureGraphic = document.createElement('div');
      signatureGraphic.className = 'print-approval-signature';
      signatureGraphic.setAttribute('aria-hidden', 'true');
      const signatureDate = document.createElement('small');
      signatureDate.className = 'print-approval-date';
      signatureDate.setAttribute('aria-hidden', 'true');
      signatureCell.append(signatureGraphic, signatureDate);
    }
  }

  function prepareApprovalPrint() {
    if (operationsTable) {
      Array.from(operationsTable.tBodies[0].rows).forEach(row => {
        ensureApprovalPrintFields(row);
        const stamp = row.querySelector('[data-stamp-select]');
        const option = stamp?.selectedOptions[0];
        const signature = row.querySelector('[name^="operationSign"]');
        const approvedAt = row.querySelector('[name^="operationApprovedAt"]')?.value;
        const stampText = option?.dataset.base || option?.textContent.split(' - ')[0] || '';
        const signatureText = option?.dataset.signature || signature?.dataset.baseSignature || '';
        const dateText = approvedAt
          ? `Approved: ${new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(`${approvedAt}T00:00:00`))}`
          : '';
        const stampGraphic = row.querySelector('.print-approval-stamp');
        stampGraphic.textContent = stampText;
        stampGraphic.classList.toggle('has-stamp', Boolean(stampText));
        const signatureGraphic = row.querySelector('.print-approval-signature');
        signatureGraphic.textContent = signatureText;
        signatureGraphic.classList.toggle('has-signature', Boolean(signatureText));
        row.querySelectorAll('.print-approval-date').forEach(date => {
          date.textContent = dateText;
          date.classList.toggle('has-date', Boolean(dateText));
        });
      });
    }
    document.querySelectorAll('[data-card-stamp-select]').forEach(select => {
      const stamp = select.closest('.e-sign-control')?.querySelector('.print-card-stamp');
      if (stamp) {
        stamp.textContent = select.selectedOptions[0]?.dataset.base || '';
        stamp.classList.toggle('has-stamp', Boolean(stamp.textContent));
      }
    });
  }

  async function loadUserStamps() {
    const response = await fetch('/account/profile');
    if (!response.ok) throw new Error(`Profile request failed (HTTP ${response.status}).`);
    const profile = await response.json();
    userStamps = Array.isArray(profile.stamps) ? profile.stamps : [];
    bindApprovalControls();
  }

  function populateStampSelect(select) {
    if (select.dataset.stampsLoaded === 'true') return;
    select.replaceChildren(new Option('', ''));
    userStamps.forEach(stamp => {
      const option = new Option(stamp.stampValue, stamp.id);
      option.dataset.base = stamp.stampValue;
      option.dataset.signature = stamp.signatureValue;
      select.add(option);
    });
    select.dataset.stampsLoaded = 'true';
  }

  function bindApprovalControls() {
    document.querySelectorAll('[data-stamp-select], [data-card-stamp-select]').forEach(select => {
      if (userStamps.length > 0) populateStampSelect(select);
      else if (select.options.length === 0) select.add(new Option('', ''));
      if (select.matches('[data-stamp-select]') && select.dataset.approvalBound !== 'true') {
        select.addEventListener('change', () => updateApproval(select.closest('tr')));
        select.dataset.approvalBound = 'true';
      }
    });
    document.querySelectorAll('[data-card-approve]').forEach(button => {
      if (button.dataset.approvalBound === 'true') return;
      button.addEventListener('click', () => {
        const key = button.dataset.cardApprove;
        const select = document.querySelector(`[name="${key}Stamp"]`);
        const date = document.querySelector(`[name="${key}ApprovedAt"]`);
        const signature = document.querySelector(`[name="${key}Sign"]`);
        const option = select?.selectedOptions[0];
        if (!select?.value || !date || !signature || !option) {
          if (saveStatus) saveStatus.textContent = 'Select an authorized stamp before applying the e-sign.';
          return;
        }
        if (!date.value) {
          const today = new Date();
          date.value = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
        }
        signature.value = `${option.dataset.signature || ''} - ${date.value}`;
        signature.dataset.baseSignature = option.dataset.signature || '';
      });
      button.dataset.approvalBound = 'true';
    });
    document.querySelectorAll('[name^="operationApprovedAt"]').forEach(date => {
      if (date.dataset.approvalBound === 'true') return;
      date.addEventListener('change', () => updateApproval(date.closest('tr')));
      date.dataset.approvalBound = 'true';
    });
    document.querySelectorAll('.approve-operation-button').forEach(button => {
      if (button.dataset.approvalBound === 'true') return;
      button.addEventListener('click', () => {
        const row = button.closest('tr');
        const date = row.querySelector('[name^="operationApprovedAt"]');
        if (!date.value) date.value = new Date().toISOString().slice(0, 10);
        updateApproval(row);
      });
      button.dataset.approvalBound = 'true';
    });
  }

  loadUserStamps().catch(error => {
    const status = document.querySelector('#stamp-load-status');
    if (status) status.textContent = `E-sign stamps could not be loaded: ${error.message}`;
  });

  function setField(name, value) {
    const field = document.querySelector(`[name="${name}"]`);
    if (!field || value === null || value === undefined) return;
    field.value = value;
    const pairedName = name === 'projectCustomer' ? 'operationProject' : name === 'operationProject' ? 'projectCustomer' : null;
    const pairedField = pairedName ? document.querySelector(`[name="${pairedName}"]`) : null;
    if (pairedField) pairedField.value = value;
  }

  const projectCustomerField = document.querySelector('[name="projectCustomer"]');
  projectCustomerField?.addEventListener('input', () => {
    const summary = document.querySelector('[name="operationProject"]');
    if (summary) summary.value = projectCustomerField.value;
  });
  document.querySelector('[name="operationProject"]')?.addEventListener('input', event => {
    if (projectCustomerField) projectCustomerField.value = event.target.value;
  });

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
      } else if (!/^operation(No|Description|DepWcr|AccQty|RejQty|Stamp|Sign|ApprovedAt)\d+$/.test(field.name)) {
        traceability.push({ field: field.name, value });
      }
    });

    try {
      const response = await fetch('/api/route-cards', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ card, traceability, operations: getOperationRows(), approvals: getApprovals() })
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

  function addInputCell(row, name, label, type, value = '') {
    const cell = document.createElement('td');
    const field = name.startsWith('operationDescription') ? document.createElement('textarea') : document.createElement('input');
    field.name = name;
    field.setAttribute('aria-label', label);
    if (field instanceof HTMLInputElement) field.type = type;
    field.value = value ?? '';
    cell.append(field);
    row.append(cell);
  }

  function addApprovalCells(row, rowNumber) {
    const stampCell = document.createElement('td');
    const stamp = document.createElement('select');
    stamp.name = `operationStamp${rowNumber}`;
    stamp.setAttribute('aria-label', `Operation ${rowNumber} stamp`);
    stamp.dataset.stampSelect = '';
    stampCell.append(stamp);
    row.append(stampCell);

    const signatureCell = document.createElement('td');
    const signature = document.createElement('input');
    signature.name = `operationSign${rowNumber}`;
    signature.setAttribute('aria-label', `Operation ${rowNumber} signature`);
    signature.readOnly = true;
    signatureCell.append(signature);
    row.append(signatureCell);

    const dateCell = document.createElement('td');
    const date = document.createElement('input');
    date.name = `operationApprovedAt${rowNumber}`;
    date.type = 'date';
    date.setAttribute('aria-label', `Operation ${rowNumber} approval date`);
    dateCell.append(date);
    row.append(dateCell);

    const approveCell = document.createElement('td');
    const approveButton = document.createElement('button');
    approveButton.type = 'button';
    approveButton.className = 'approve-operation-button';
    approveButton.textContent = 'Approve';
    approveButton.setAttribute('aria-label', `Approve operation ${rowNumber}`);
    approveCell.append(approveButton);
    row.append(approveCell);
    ensureApprovalPrintFields(row);
  }

  if (operationsTable) operationsTable.tBodies[0].querySelectorAll('tr').forEach(ensureApprovalPrintFields);

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
        ['operationAccQty', 'accepted quantity', 'text'],
        ['operationRejQty', 'rejected quantity', 'text']
      ];
      fields.forEach(([name, label, type]) => addInputCell(row, `${name}${rowNumber}`, `Operation ${displayNumber} ${label}`, type));
      addApprovalCells(row, rowNumber);
      tbody.append(row);
      bindApprovalControls();
    });
  });

  function populateOperations(operations, clearWhenEmpty = false) {
    if (!operationsTable || !Array.isArray(operations)) return;
    if (operations.length === 0 && !clearWhenEmpty) return;
    const tbody = operationsTable.tBodies[0];
    tbody.innerHTML = '';
    operations.forEach((operation, index) => {
      const row = document.createElement('tr');
      const rowNumber = index + 1;
      addInputCell(row, `operationNo${rowNumber}`, `Operation ${rowNumber} number`, 'text', operation.opNo);
      addInputCell(row, `operationDescription${rowNumber}`, `Operation ${rowNumber} description`, 'text', operation.operationName);
      addInputCell(row, `operationDepWcr${rowNumber}`, `Operation ${rowNumber} department or WCR`, 'text', operation.workcenterDepartment);
      addInputCell(row, `operationAccQty${rowNumber}`, `Operation ${rowNumber} accepted quantity`, 'text');
      addInputCell(row, `operationRejQty${rowNumber}`, `Operation ${rowNumber} rejected quantity`, 'text');
      addApprovalCells(row, rowNumber);
      tbody.append(row);
    });
    bindApprovalControls();
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
    MATERIAL_DATE: 'materialDate', REFERENCE_DRAWING_NO: 'referenceDrawingNo', PROJECT: 'projectCustomer', PROJECT_NAME: 'projectCustomer',
    PRODUCT_TYPE: 'deptProductType', DEPT_PRODUCT_TYPE: 'deptProductType', DETP: 'deptProductType', DETP_PRODUCT_TYPE: 'deptProductType',
    RAW_MATERIAL_DIA: 'rawMaterialDia', RAW_MATERIAL_LENGTH: 'rawMaterialLength', RAW_MATERIAL_BATCH_NO: 'rawMaterialBatchNo',
    ALTERNATIVE_MATERIAL_SIZE: 'alternativeMaterialSizeDia', RUBBER_SPECIFICATION: 'rubberSpecification', RUBBER_QTY: 'rubberQty', RUBBER_SUPPLIER: 'rubberSupplier',
    TA_PC_REF: 'taPcRef', BS_WEIGHT: 'bsWeight', FORGING_WEIGHT: 'forgingWeight', FORGING_PLANT: 'forgingPlant', FORGING_BATCH_NO: 'forgingBatchNo', FORGING_QTY: 'forgingQty',
    RUBBER_BATCH_NO: 'rubberBatchNo', RUBBER_LTR_NO: 'rubberLtrNo', UN_FETT: 'unFett', FETT_WEIGHT: 'fettWeight', CASTING_BATCH_NO: 'castingBatchNo', CASTING_QTY: 'castingQty',
    PAD_WEIGHT: 'padWeight', PLATING_BATCH_NO: 'platingBatchNo', MIX_BATCH_NO: 'mixBatchNo', POWDER_QTY: 'powderQty'
  };

  function setMatchedFields(fields) {
    Object.entries(fields || {}).forEach(([name, value]) => setField(fieldAliases[name.toUpperCase()] || name, value));
  }

  async function searchWorkOrder() {
    const workOrderNo = document.querySelector('[name="workOrderNo"]')?.value.trim();
    const status = document.querySelector('#work-order-search-status');
    const button = document.querySelector('#search-work-order-button');
    if (!workOrderNo) {
      if (status) status.textContent = 'Enter a work order number.';
      return;
    }
    if (button) button.disabled = true;
    if (status) status.textContent = 'Searching...';
    try {
      const response = await fetch(`/api/route-cards/lookup?workOrderNo=${encodeURIComponent(workOrderNo)}`);
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || data.detail || 'No matching data was found.');
      setField('orderNo', data.orderNo);
      setField('workOrderNo', data.workOrderNo || workOrderNo);
      setField('partNo', data.partNo);
      setField('partName', data.partName);
      setField('pslNo', data.pslNo);
      setField('qty', data.qty);
      setField('jcQty', data.jcQty ?? data.qty);
      setField('operationPslNo', data.pslNo);
      setField('operationWorkOrderNo', data.workOrderNo || workOrderNo);
      setField('operationJcQty', data.jcQty ?? data.qty);
      setMatchedFields(data.matchedFields);
      populateOperations(data.operations, Boolean(data.operationsMessage));
      if (status) status.textContent = data.operationsMessage || `Loaded data from ${data.sourceTable || 'RouteCard lookup'} (${data.operations?.length || 0} operations).`;
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
      populateOperations(data.operations, Boolean(data.operationsMessage));
      if (status) status.textContent = data.operationsMessage || `Loaded ${data.records?.length || 0} matching rows and ${data.operations?.length || 0} operations from ${(data.sourceDatabases || []).filter((value, index, values) => values.indexOf(value) === index).join(', ') || 'HALFNF/HALNEW'}.`;
    } catch (error) {
      if (status) status.textContent = error.message;
    } finally {
      if (button) button.disabled = false;
    }
  }

  document.querySelector('#search-work-order-button')?.addEventListener('click', searchWorkOrder);
  document.querySelector('[name="workOrderNo"]')?.addEventListener('keydown', event => {
    if (event.key === 'Enter') {
      event.preventDefault();
      searchWorkOrder();
    }
  });
  document.querySelector('#search-part-button')?.addEventListener('click', searchPart);
  document.querySelector('[name="partNo"]')?.addEventListener('keydown', event => {
    if (event.key === 'Enter') {
      event.preventDefault();
      searchPart();
    }
  });

  document.querySelector('#print-route-card')?.addEventListener('click', () => {
    prepareApprovalPrint();
    window.print();
  });
  window.addEventListener('beforeprint', prepareApprovalPrint);
});