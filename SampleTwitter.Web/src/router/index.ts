import { createRouter, createWebHistory } from 'vue-router';
import HomeView from '@/views/HomeView.vue';
import SignUpView from '@/views/SignUpView.vue';
import LoginView from '@/views/LoginView.vue';
import ConfirmEmailView from '@/views/ConfirmEmailView.vue';
import ForgotPasswordView from '@/views/ForgotPasswordView.vue';
import ResetPasswordView from '@/views/ResetPasswordView.vue';
import ProfileView from '@/views/ProfileView.vue';
import PostDetailView from '@/views/PostDetailView.vue';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomeView,
    },
    {
      path: '/signup',
      name: 'signup',
      component: SignUpView,
    },
    {
      path: '/login',
      name: 'login',
      component: LoginView,
    },
    {
      path: '/forgot-password',
      name: 'forgot-password',
      component: ForgotPasswordView,
    },
    {
      path: '/reset-password',
      name: 'reset-password',
      component: ResetPasswordView,
    },
    {
      path: '/confirm-email',
      name: 'confirm-email',
      component: ConfirmEmailView,
    },
    {
      path: '/profile/:id',
      name: 'user-profile',
      component: ProfileView,
    },
    {
      path: '/profile',
      name: 'profile',
      redirect: () => {
        const storedUserId = localStorage.getItem('sampletwitter_user_id');
        return storedUserId ? `/profile/${storedUserId}` : '/login';
      },
    },
    {
      path: '/post/:id',
      name: 'post-detail',
      component: PostDetailView,
    },
  ],
});

export default router;