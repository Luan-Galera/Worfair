import { http } from '../http';

// Camada de endpoints por feature. Páginas nunca instanciam Axios diretamente.
export const jobsApi = {
  listPostings: (params) => http.get('/jobs/postings', { params }),
  getPosting: (id) => http.get(`/jobs/postings/${id}`),
  createPosting: (payload) => http.post('/jobs/postings', payload),
  publishPosting: (id) => http.post(`/jobs/postings/${id}/publish`),
  applyToJob: (id, payload) => http.post(`/jobs/postings/${id}/applications`, payload),
  myApplications: () => http.get('/jobs/postings/applications/me'),
  listProjects: (params) => http.get('/jobs/projects', { params }),
  getProject: (id) => http.get(`/jobs/projects/${id}`),
  createProject: (payload) => http.post('/jobs/projects', payload),
  openProject: (id) => http.post(`/jobs/projects/${id}/open`),
};

export const proposalsApi = {
  list: () => http.get('/proposals'),
  mine: () => http.get('/proposals/mine'),
  submit: (payload) => http.post('/proposals', payload),
  decide: (id, accept) => http.post(`/proposals/${id}/decide`, { accept }),
};

export const financialApi = {
  invoices: () => http.get('/financial/invoices'),
  invoiceDetail: (id) => http.get(`/financial/invoices/${id}`),
  balance: () => http.get('/financial/balance'),
  revenue: () => http.get('/financial/revenue'),
  createInvoice: (payload) => http.post('/financial/invoices', payload),
  settleInvoice: (id) => http.post(`/financial/invoices/${id}/settle`),
  chargeInvoice: (id, payload) => http.post(`/financial/invoices/${id}/charge`, payload),
  invoicePayment: (id) => http.get(`/financial/invoices/${id}/payment`),
  adminInvoices: () => http.get('/financial/admin/invoices'),
  adminCreateInvoice: (payload) => http.post('/financial/admin/invoices', payload),
  adminSettle: (id) => http.post(`/financial/admin/invoices/${id}/settle`),
  disputes: () => http.get('/financial/disputes'),
  getDispute: (id) => http.get(`/financial/disputes/${id}`),
  openDispute: (payload) => http.post('/financial/disputes', payload),
  resolveDispute: (id, accepted) =>
    http.post(`/financial/disputes/${id}/resolve`, null, { params: { accepted } }),
};

export const messagesApi = {
  byInvoice: (invoiceId) => http.get(`/messages/invoices/${invoiceId}`),
  send: (payload) => http.post('/messages', payload),
};

export const notificationsApi = {
  list: () => http.get('/notifications'),
  markRead: (id) => http.post(`/notifications/${id}/read`),
};

export const tenantsApi = {
  listPlatform: () => http.get('/platform/tenants'),
  provision: (payload) => http.post('/platform/tenants', payload),
  companies: () => http.get('/tenants/companies'),
  createCompany: (payload) => http.post('/tenants/companies', payload),
  members: () => http.get('/tenants/members'),
  addMember: (targetUserId) => http.post('/tenants/members', { targetUserId }),
  addMemberByEmail: (email) => http.post('/tenants/members/by-email', { email }),
  transferCompanyOwner: (companyId, newOwnerUserId) =>
    http.patch(`/tenants/companies/${companyId}/owner`, { newOwnerUserId }),
  assignRole: (targetUserId, roleCode) =>
    http.post(`/identity/users/${targetUserId}/roles`, { roleCode }),
  removeRole: (targetUserId, roleCode) =>
    http.delete(`/identity/users/${targetUserId}/roles/${roleCode}`),
};

export const TOKEN_CLAIM_TENANT = 'tenant_id';
